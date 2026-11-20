using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class LibraryBugTests
    {
        private static readonly ISqlFunction[] Twice =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
        ];

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Pay IN (2, 3)", "Bortus, Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay NOT IN (2, 3)", "Ed Mercer, Kelly Grayson, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Pay IN (3, 4.5)", "Kelly Grayson, Bortus")]
        public void InListsCompareTypedDecimalsExactly(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result.Select(c => c.Name)));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Name > 'K'", "Kelly Grayson, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Name <= 'Bortus'", "Bortus")]
        [DataRow("SELECT * FROM this WHERE Name BETWEEN 'B' AND 'H'", "Ed Mercer, Bortus, Gordon Malloy")]
        public void StringsCompareInOrder(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result.Select(c => c.Name)));
        }

        [TestMethod]
        [DataRow("SELECT MIN(Name) FROM this", "Bortus")]
        [DataRow("SELECT MAX(Name) FROM this WHERE Species = 'Human'", "Kelly Grayson")]
        [DataRow("SELECT MIN(Species) FROM this WHERE Rank = 3", "Moclan")]
        public void MinAndMaxReadStrings(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().QueryScalar<CrewMember, string>(sql);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE Name = (SELECT MIN(Name) FROM crew)", "Bortus/Moclan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Name = (SELECT MAX(Name) FROM crew WHERE Species = 'Human')", "Kelly Grayson/Human")]
        [DataRow("SELECT Name, Class FROM ships WHERE Name = (SELECT MIN(Name) FROM ships)", "Orville/Exploratory")]
        public void MinAndMaxSubqueriesReadStrings(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(ShipId) FROM crew GROUP BY Species ORDER BY Species", "Human/3; Kaylon/0; Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(ShipId) = 0", "Kaylon/1")]
        [DataRow("SELECT s.Name, COUNT(c.Name) FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Pembroke/0; Roosevelt/1")]
        public void CountOfAnExpressionSkipsNullsInGroups(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT COUNT(ShipId) FROM this", 5)]
        [DataRow("SELECT COUNT(Name) FROM this WHERE Rank > 3", 2)]
        public void CountOfAnExpressionSkipsNulls(string sql, int expected)
        {
            // Act
            var result = Orville.Crew().QueryScalar<CrewMember, int>(sql);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew WHERE Rank < 5 GROUP BY Species, ShipId HAVING COUNT(*) > 1", "Human/2")]
        [DataRow("SELECT c.Species, MAX(c.Age) FROM crew c GROUP BY c.Species, c.ShipId HAVING MAX(c.Age) > 40 ORDER BY c.Species", "Human/41; Kaylon/200; Moclan/45")]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name, c.Species HAVING COUNT(*) > 1", "Orville/3")]
        public void GroupByColumnsNeedNotBeSelected(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void GroupedCountsNeedNotSelectTheGroup()
        {
            // Act
            var result = Orville.Tables().Query<int>("SELECT COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > 1");

            // Assert
            Assert.AreEqual("3", string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Species FROM crew GROUP BY Species ORDER BY Species", "Human, Kaylon, Moclan, Xelayan")]
        [DataRow("SELECT Species FROM crew GROUP BY Species HAVING COUNT(*) > 1", "Human")]
        [DataRow("SELECT s.Class FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Class", "Exploratory")]
        public void GroupByNeedsNoSelectedAggregate(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) AS Total FROM crew GROUP BY Species ORDER BY Total DESC, Species", "Human/3; Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT s.Name, COUNT(*) AS Total FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name ORDER BY Total DESC, s.Name", "Orville/4; Pembroke/1; Roosevelt/1")]
        public void OrderByReadsAggregateAliases(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species AS Kind FROM crew WHERE Rank < 4 ORDER BY Kind DESC, Name", "Talla Keyali/Xelayan; Bortus/Moclan; Isaac/Kaylon; Gordon Malloy/Human")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 3 ORDER BY Ship DESC", "Talla Keyali/Roosevelt; Bortus/Orville")]
        public void OrderByReadsColumnAliases(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void GroupColumnsTakeTheirAlias()
        {
            // Act
            var row = Orville.Tables().Query("SELECT Species AS Kind, COUNT(*) AS Total FROM crew GROUP BY Species ORDER BY Species").First();
            var posting = Orville.Tables().Query<Posting>("SELECT Species AS Name, COUNT(*) AS Rank FROM crew GROUP BY Species ORDER BY Species").First();
            var joined = Orville.Tables().Query<SpeciesTally>("SELECT c.Species, COUNT(*) AS Total FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY c.Species HAVING Total > 1").Single();

            // Assert
            Assert.AreEqual("Human", row["Kind"]);
            Assert.AreEqual("Human", posting.Name);
            Assert.AreEqual(3, posting.Rank);
            Assert.AreEqual("Human", joined.Species);
            Assert.AreEqual(3, joined.Total);
        }

        [TestMethod]
        [DataRow("SELECT c.Species, COUNT(DISTINCT s.Name) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY c.Species ORDER BY c.Species", "Human/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT s.Name, COUNT(DISTINCT c.Name) FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Pembroke/0; Roosevelt/1")]
        [DataRow("SELECT s.Name, COUNT(ALL c.Name) FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Pembroke/0; Roosevelt/1")]
        public void JoinAggregatesReadTheQualifiedField(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, int>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        public void TheSameFieldCanBeSelectedTwice()
        {
            // Act
            var row = Orville.Tables().Query("SELECT Name, Name AS Again FROM crew WHERE Rank = 5").Single();
            var tuple = Orville.Tables().Query<Tuple<string, string>>("SELECT Post, Post AS Duty FROM duties WHERE Post = 'Helm'").Single();

            // Assert
            Assert.AreEqual("Ed Mercer", row["Again"]);
            Assert.AreEqual(Tuple.Create("Helm", "Helm"), tuple);
        }

        [TestMethod]
        [DataRow("SELECT [c].[Name], c.[Species] FROM crew c WHERE [c].[Rank] = 5", "Ed Mercer/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.[Rank] = 5", "Ed Mercer/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank > 3 ORDER BY [c].[Name] DESC", "Kelly Grayson/Human; Ed Mercer/Human")]
        [DataRow("SELECT [c].[Name], [s].[Name] AS Ship FROM crew c JOIN ships s ON [c].[ShipId] = [s].[Id] WHERE [s].[Id] = 2", "Talla Keyali/Roosevelt")]
        public void BracketedQualifiedNamesResolve(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Age / 2 = 22", "Bortus")]
        [DataRow("SELECT Name FROM crew WHERE Age / 10 = 4", "Ed Mercer, Kelly Grayson, Bortus")]
        public void IntegerDivisionMatchesAcrossRowShapes(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, string>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql);
            var expandos = Orville.CrewExpandos().Query<dynamic, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed), "typed");
            Assert.AreEqual(expected, string.Join(", ", dictionaries), "dictionaries");
            Assert.AreEqual(expected, string.Join(", ", expandos), "expandos");
        }

        [TestMethod]
        public void ExpandoListTablesReadFields()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["crew"] = Orville.CrewExpandos() };

            // Act
            var result = tables.Query<Tuple<string, string>>("SELECT c.Name, c.Species FROM crew c WHERE c.Pay > 3 AND c.Species <> 'Human' OR c.ShipId IS NULL ORDER BY c.Name");

            // Assert
            Assert.AreEqual("Isaac/Kaylon; Talla Keyali/Xelayan", Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) FROM this INNER JOIN that ON this.ShipId = that.Id WHERE that.Name = 'Orville'", 4)]
        [DataRow("SELECT COUNT(*) FROM this LEFT JOIN that ON this.ShipId = that.Id", 6)]
        [DataRow("SELECT COUNT(*) FROM this RIGHT JOIN that ON this.ShipId = that.Id", 6)]
        [DataRow("SELECT COUNT(*) FROM this c JOIN that s ON s.Id = c.ShipId WHERE s.Class = 'Exploratory' AND c.Rank < 4", 3)]
        public void ScalarJoinQueriesReturnOneValue(string sql, int expected)
        {
            // Act
            var result = Orville.Crew().QueryScalar<CrewMember, Ship, int>(sql, Orville.Ships());

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE (SELECT COUNT(*) FROM ships) IS NOT NULL AND Rank = 5", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE (SELECT MAX(Rank) FROM crew) IS NULL OR Rank = 5", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE (SELECT MIN(Age) FROM crew) IS NOT NULL AND Rank = 4", "Kelly Grayson")]
        public void AggregateSubqueriesCompareWithNull(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE (SELECT MAX(Id) FROM ships WHERE Class = 'Warship') IS NULL AND Rank = 5", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE (SELECT MIN(Moons) FROM planets WHERE Gravity > 5) IS NULL AND Rank = 4", "Kelly Grayson")]
        public void EmptyAggregateSubqueriesAreNull(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        public void MinMaxAndAvgOverNoRowsReturnNull()
        {
            // Arrange
            var crew = Orville.Crew();

            // Act
            var max = crew.Query<CrewMember, int?>("SELECT MAX(Rank) FROM this WHERE Rank > 10").Single();
            var min = crew.Query<CrewMember, int?>("SELECT MIN(Age) FROM this WHERE Species = 'Vulcan'").Single();
            var avg = crew.Query<CrewMember, double?>("SELECT AVG(Rank) FROM this WHERE Rank > 10").Single();

            // Assert
            Assert.IsNull(max);
            Assert.IsNull(min);
            Assert.IsNull(avg);
        }

        [TestMethod]
        public void SumOverNoRowsReturnsNull()
        {
            // Act
            var sum = Orville.Crew().Query<CrewMember, int?>("SELECT SUM(Rank) FROM this WHERE Rank > 10").Single();
            var matched = Orville.Tables().Query<string>("SELECT Name FROM crew WHERE (SELECT SUM(Id) FROM ships WHERE Id > 9) = 0");

            // Assert
            Assert.IsNull(sum);
            Assert.IsEmpty(matched.ToList());
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE (SELECT COUNT(*) FROM crew c WHERE c.Species = crew.Species) = 1", "Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay = (SELECT MAX(c.Pay) FROM crew c WHERE c.Species = crew.Species)", "Ed Mercer/Human; Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM this WHERE Rank = (SELECT MAX(c.Rank) FROM this c WHERE c.Species = this.Species)", "Ed Mercer/Human; Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        public void OuterTableNamesBindToTheOuterRow(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING Species <> 'Human' ORDER BY Species DESC", "Xelayan/1; Moclan/1; Kaylon/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING c.Species LIKE 'K%'", "Kaylon/1")]
        [DataRow("SELECT s.Class, COUNT(*) FROM ships s GROUP BY s.Class HAVING (SELECT k.Role FROM classes k WHERE k.Class = s.Class) = 'Research'", "Science/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING (SELECT p.Moons FROM planets p WHERE p.Species = c.Species) > 1 ORDER BY c.Species", "Kaylon/1; Xelayan/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING (SELECT COUNT(*) FROM planets p WHERE p.Species = c.Species AND p.Moons = 1) = 1", "Human/3")]
        public void HavingWithoutAnAggregateReadsTheGroup(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE Rank = (SELECT MAX(twice(Rank)) FROM this) / 2", "Ed Mercer")]
        [DataRow("SELECT c.Name FROM this c WHERE twice(c.Rank) = (SELECT MAX(twice(c2.Rank)) FROM this c2 WHERE c2.Species = c.Species)", "Ed Mercer, Bortus, Talla Keyali, Isaac")]
        [DataRow("SELECT Name FROM this WHERE Age > (SELECT AVG(twice(Age)) FROM this WHERE Species = 'Human')", "Isaac")]
        public void FunctionsComposeInsideAggregates(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, string>(sql, Twice);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT x.Name FROM crew c", "x.Name can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c ORDER BY x.Name", "x.Name can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING x.Species = 'Human'", "x.Species can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING MAX(x.Rank) > 3", "x.Rank can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c WHERE x.Rank > 3", "x.Rank can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE x.Rank > 3", "x.Rank can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY x.Name", "x.Name can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT x.Name FROM crew c, ships s WHERE c.ShipId = s.Id", "x.Name can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank > (SELECT MAX(x.Rank) FROM crew c2)", "x.Rank can't be read because x isn't a source or alias of this query")]
        public void UnknownAliasesThrowSqlExceptionEverywhere(string sql, string message)
        {
            // Act
            var typed = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            var dictionaries = Assert.ThrowsExactly<SqlException>(() => Orville.DictionaryTables().Query(sql).ToList());

            // Assert
            Assert.Contains(message, typed.Message);
            Assert.Contains(message, dictionaries.Message);
        }

        [TestMethod]
        [DataRow("SELECT L * F AS v FROM this", 7.5f)]
        [DataRow("SELECT L + F AS v FROM this", 6.5f)]
        [DataRow("SELECT F - L AS v FROM this", -3.5f)]
        public void LongWithFloatKeepsTheFraction(string sql, float expected)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { L = 5L, F = 1.5f } } };

            // Act
            var value = tables.Query(sql).Single()["v"];
            var matched = tables.Query("SELECT L FROM this WHERE L * F = 7.5").Count();

            // Assert
            Assert.AreEqual(expected, value);
            Assert.AreEqual(1, matched);
        }

        [TestMethod]
        public void UnsignedIntegersAreNotNarrowed()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { U = 4000000000u } } };

            // Act
            var matched = tables.Query("SELECT U FROM this WHERE U > 1").Count();
            var value = tables.Query("SELECT U + 1 AS v FROM this").Single()["v"];

            // Assert
            Assert.AreEqual(1, matched);
            Assert.AreEqual(4000000001L, value);
        }

        [TestMethod]
        public void SmallIntegersPromoteToInt()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { B = (byte)200, S = (short)30000 } } };

            // Act
            var row = tables.Query("SELECT -B AS n, B + B AS b, S + S AS s FROM this").Single();

            // Assert
            Assert.AreEqual(-200, row["n"]);
            Assert.AreEqual(400, row["b"]);
            Assert.AreEqual(60000, row["s"]);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM nosuch")]
        [DataRow("SELECT n.Name FROM nosuch n")]
        [DataRow("SELECT c.Name FROM crew c, nosuch n")]
        public void UnknownTablesThrowEvenWithAThisTable(string sql)
        {
            // Arrange
            var tables = Orville.Tables();
            tables["this"] = Orville.Ships();
            var single = new Dictionary<string, IEnumerable> { ["crew"] = Orville.Crew() };

            // Act
            var many = Assert.ThrowsExactly<SqlException>(() => tables.Query(sql).ToList());
            var one = Assert.ThrowsExactly<SqlException>(() => single.Query(sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.UnknownName, many.Kind);
            Assert.AreEqual(SqlErrorKind.UnknownName, one.Kind);
            Assert.AreEqual(sql.IndexOf("nosuch", StringComparison.Ordinal), many.Start);
            Assert.AreEqual(sql.IndexOf("nosuch", StringComparison.Ordinal), one.Start);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '^start'", "^start")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '%^%'", "^start, [^ab")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '%[%'", "[abc], [^ab")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '[%'", "[abc], [^ab")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '[abc%'", "[abc]")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '[^a%'", "[^ab")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '%]'", "[abc], a]")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '[^a]%'", "^start, [abc], [^ab")]
        [DataRow("SELECT Name FROM this WHERE Name LIKE '[ab]%'", "abc, a]")]
        public void LikeReadsCaretsAndUnclosedBracketsLiterally(string sql, string expected)
        {
            // Arrange
            var ships = new List<Ship> { new Ship { Name = "^start" }, new Ship { Name = "abc" }, new Ship { Name = "[abc]" }, new Ship { Name = "a]" }, new Ship { Name = "[^ab" } };

            // Act
            var result = ships.Query<Ship, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships WHERE Id = 1)", "Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE NOT ShipId IN (SELECT Id FROM ships WHERE Id = 1)", "Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE NOT (ShipId IN (SELECT Id FROM ships WHERE Id = 1))", "Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE ShipId NOT IN (1)", "Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE NOT ShipId IN (1)", "Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE NOT (ShipId IN (1))", "Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships WHERE Id > 5)", "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE ShipId NOT IN (SELECT ShipId FROM crew WHERE Rank = 2)", "")]
        [DataRow("SELECT Name FROM crew WHERE ShipId NOT IN (1, NULL)", "")]
        [DataRow("SELECT Name FROM crew WHERE ShipId IN (1, NULL) AND Rank > 3", "Ed Mercer, Kelly Grayson")]
        public void NotInTreatsNullTheSameWayInEveryForm(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<string>(sql);
            var dictionaries = Orville.DictionaryTables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        public void DateLiteralsReadTheSameInEveryCulture()
        {
            // Arrange
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");

            try
            {
                // Act
                var date = SqlExpression.Compile<CrewMember>("#1/2/2000#")(null);

                // Assert
                Assert.AreEqual(new DateTime(2000, 1, 2), date);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestMethod]
        [DataRow("#2000-13-45#")]
        [DataRow("#notadate#")]
        public void InvalidDateLiteralsThrowSqlException(string literal)
        {
            // Arrange
            var sql = "SELECT Name FROM this WHERE " + literal + " IS NULL";

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, string>(sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.Lexical, e.Kind);
            Assert.AreEqual(sql.IndexOf('#', StringComparison.Ordinal), e.Start);
            Assert.AreEqual(literal.Length, e.Length);
        }

        [TestMethod]
        public void StarSelectsTypedRowsIntoDictionaries()
        {
            // Act
            var tables = Orville.Tables().Query("SELECT * FROM crew WHERE Rank = 5").Single();
            var query = Orville.Crew().Query<CrewMember, IDictionary<string, object>>("SELECT * FROM this WHERE Rank = 4").Single();

            // Assert
            Assert.AreEqual("Name, Species, Rank, Age, Pay, ShipId", string.Join(", ", tables.Keys));
            Assert.AreEqual("Ed Mercer", tables["Name"]);
            Assert.AreEqual("Kelly Grayson", query["name"]);
        }

        [TestMethod]
        [DataRow("SELECT Warp FROM this")]
        [DataRow("SELECT Name FROM this WHERE Warp > 1")]
        [DataRow("SELECT Name FROM this ORDER BY Warp")]
        [DataRow("SELECT Species FROM this GROUP BY Species, Warp")]
        public void UnknownTypedFieldsThrowSqlException(string sql)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, string>(sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.UnknownName, e.Kind);
            Assert.AreEqual(sql.IndexOf("Warp", StringComparison.Ordinal), e.Start);
            Assert.AreEqual(4, e.Length);
        }

        [TestMethod]
        [DataRow("SELECT 10000000000 AS v FROM this", 10000000000L)]
        [DataRow("SELECT -2147483648 AS v FROM this", -2147483648L)]
        [DataRow("SELECT 3000000000 AS v FROM this", 3000000000L)]
        public void WholeLiteralsBeyondInt32AreLong(string sql, long expected)
        {
            // Act
            var value = Orville.Crew().Query<CrewMember, object>(sql).First();

            // Assert
            Assert.AreEqual(expected, value);
        }

        [TestMethod]
        public void WholeLiteralsBeyondInt64AreDouble()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { Big = 10000000000L } } };

            // Act
            var value = tables.Query("SELECT 100000000000000000000 AS v FROM this").Single()["v"];
            var matched = tables.Query("SELECT Big FROM this WHERE Big = 10000000000").Count();

            // Assert
            Assert.AreEqual(1e20, value);
            Assert.AreEqual(1, matched);
        }

        [TestMethod]
        public void MinAndMaxReadTypedValuesThatAreNotNumbers()
        {
            // Arrange
            var rows = new[]
            {
                new { D = new DateTime(2001, 1, 1), B = true, C = 'b', S = (short)2, T = TimeSpan.FromHours(2), G = new Guid("00000000-0000-0000-0000-000000000002") },
                new { D = new DateTime(2000, 1, 1), B = false, C = 'a', S = (short)1, T = TimeSpan.FromHours(1), G = new Guid("00000000-0000-0000-0000-000000000001") },
            };
            var tables = new Dictionary<string, IEnumerable> { ["this"] = rows };

            // Act
            var row = tables.Query("SELECT MIN(D) AS d, MAX(B) AS b, MIN(C) AS c, MAX(S) AS s, MIN(T) AS t, MAX(G) AS g FROM this").Single();

            // Assert
            Assert.AreEqual(new DateTime(2000, 1, 1), row["d"]);
            Assert.AreEqual(true, row["b"]);
            Assert.AreEqual('a', row["c"]);
            Assert.AreEqual((short)2, row["s"]);
            Assert.AreEqual(TimeSpan.FromHours(1), row["t"]);
            Assert.AreEqual(new Guid("00000000-0000-0000-0000-000000000002"), row["g"]);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE ShipId = '1'", "ShipId = '1'")]
        [DataRow("SELECT Name FROM this WHERE Name = 1", "Name = 1")]
        [DataRow("SELECT Name + 'x' AS v FROM this", "Name + 'x'")]
        [DataRow("SELECT -Name AS v FROM this", "-Name")]
        [DataRow("SELECT Name FROM this WHERE Pay BETWEEN 'a' AND 'b'", "Pay BETWEEN 'a' AND 'b'")]
        [DataRow("SELECT Name FROM this WHERE Rank IN (1, 'a')", "1, 'a'")]
        [DataRow("SELECT Name FROM this WHERE ShipId IN ('a', 'b')", "ShipId IN ('a', 'b')")]
        [DataRow("SELECT Name FROM this WHERE NOT Name", "NOT Name")]
        public void TypeMismatchesOnTypedRowsThrowSqlException(string sql, string text)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, string>(sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.Semantic, e.Kind);
            Assert.AreEqual(text, sql.Substring(e.Start, e.Length));
        }

        [TestMethod]
        [DataRow("SELECT N FROM this WHERE Day = 'Monday'", "Day = 'Monday'")]
        [DataRow("SELECT N FROM this WHERE G = 'abc'", "G = 'abc'")]
        [DataRow("SELECT N FROM this WHERE C = 'a'", "C = 'a'")]
        [DataRow("SELECT N FROM this WHERE O > #1/1/2000#", "O > #1/1/2000#")]
        [DataRow("SELECT N FROM this WHERE Day", "Day")]
        [DataRow("SELECT N FROM this WHERE NOT Day", "NOT Day")]
        public void TypeMismatchesOfOtherTypesThrowSqlException(string sql, string text)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { N = 1, Day = DayOfWeek.Monday, G = Guid.Empty, C = 'a', O = DateTimeOffset.MinValue } } };

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => tables.Query(sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.Semantic, e.Kind);
            Assert.AreEqual(text, sql.Substring(e.Start, e.Length));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE ShipId", "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE Rank LIKE '3%'", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE ShipId LIKE '2'", "Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE NOT ShipId OR Rank = 5", "Ed Mercer")]
        [DataRow("SELECT Name FROM this WHERE ShipId AND Rank = 2", "Gordon Malloy")]
        public void TypedRowsReadLikeDictionaryRows(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, string>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        public void NullableBooleansAreConditions()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { N = 1, Flag = (bool?)true }, new { N = 2, Flag = (bool?)false }, new { N = 3, Flag = (bool?)null } } };

            // Act
            var result = tables.Query<int>("SELECT N FROM this WHERE Flag");

            // Assert
            Assert.AreEqual("1", string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT mod(Rank, 2) AS r, COUNT(*) AS n FROM this GROUP BY mod(Rank, 2)", "1/3; 0/3")]
        [DataRow("SELECT mod(Rank, 2) + 1 AS r, COUNT(*) AS n FROM this GROUP BY mod(Rank, 2)", "2/3; 1/3")]
        public void GroupByAFunctionGroupsByItsValue(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Tuple<int, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this ORDER BY abs(Rank)", "abs(Rank)")]
        [DataRow("SELECT DISTINCT Name FROM this ORDER BY abs(Rank) DESC", "abs(Rank)")]
        [DataRow("SELECT Name FROM this ORDER BY abs(Rank), Name", "abs(Rank)")]
        public void OrderByAFunctionThrowsSqlException(string sql, string text)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, string>(sql).ToList());
            var syntax = SqlSyntaxTree.Parse(sql);

            // Assert
            Assert.AreEqual(text, sql.Substring(e.Start, e.Length));
            Assert.IsFalse(e.Message.Contains("$sqlfn", StringComparison.Ordinal), e.Message);
            Assert.IsNotNull(syntax.Root);
            Assert.AreEqual(e.Message, syntax.Error.Message);
        }

        [TestMethod]
        public void JoinOnAFunctionThrowsSqlException()
        {
            // Arrange
            const string Sql = "SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = abs(s.Id)";

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(Sql).ToList());
            var syntax = SqlSyntaxTree.Parse(Sql);

            // Assert
            Assert.AreEqual("JOIN ON can't call abs(s.Id) because it compares column names, not function calls.", e.Message);
            Assert.AreEqual("abs(s.Id)", Sql.Substring(e.Start, e.Length));
            Assert.AreEqual(e.Message, syntax.Error.Message);
        }

        [TestMethod]
        public void QueryScalarReadsStarAndNull()
        {
            // Arrange
            var crew = Orville.Crew();
            var none = new List<CrewMember>();

            // Act
            var first = crew.QueryScalar<CrewMember>("SELECT * FROM this");
            var missing = none.QueryScalar<CrewMember, double?>("SELECT AVG(Age) FROM this");
            var e = Assert.ThrowsExactly<InvalidOperationException>(() => none.QueryScalar<CrewMember, double>("SELECT AVG(Age) FROM this"));

            // Assert
            Assert.AreSame(crew[0], first);
            Assert.IsNull(missing);
            Assert.Contains("Double", e.Message);
        }

        [TestMethod]
        public void EvaluateBeforeCompileThrowsInvalidOperationException()
        {
            // Arrange
            var crew = Orville.Crew();

            // Act
            var query = Assert.ThrowsExactly<InvalidOperationException>(() => new Query<CrewMember, string>("SELECT Name FROM this").Evaluate(crew));
            var scalar = Assert.ThrowsExactly<InvalidOperationException>(() => new ScalarQuery<CrewMember, int>("SELECT COUNT(*) FROM this").Evaluate(crew));

            // Assert
            Assert.Contains("A query can't be evaluated until it has been compiled.", query.Message);
            Assert.Contains("A query can't be evaluated until it has been compiled.", scalar.Message);
        }

        [TestMethod]
        public void ResultTypesWithoutAColumnThrowSqlException()
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, Ship>("SELECT Name, Rank FROM this").ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.Semantic, e.Kind);
            Assert.Contains("Rank", e.Message);
        }

        [TestMethod]
        public void OneColumnFillsAMatchingProperty()
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, SpeciesTally>("SELECT Rank AS Total FROM this WHERE Rank = 5").Single();

            // Assert
            Assert.AreEqual(5, result.Total);
        }

        [TestMethod]
        public void OneColumnStillConvertsToAResultTypeWithAConversion()
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Ranked>("SELECT Rank FROM this WHERE Rank = 5").Single();

            // Assert
            Assert.AreEqual(50, result.Rank);
        }

        [TestMethod]
        public void FunctionMistakesThrowClearExceptions()
        {
            // Arrange
            var none = new ISqlFunction[] { new TestFunction("nothing", arguments => null) };
            var twins = new ISqlFunction[] { new TestFunction("twin", arguments => arguments[0].Expression), new TestFunction("TWIN", arguments => arguments[0].Expression) };

            // Act
            var empty = Assert.ThrowsExactly<InvalidOperationException>(() => Orville.Crew().Query<CrewMember, int>("SELECT nothing(Rank) AS r FROM this", none).ToList());
            var same = Assert.ThrowsExactly<ArgumentException>(() => Orville.Crew().Query<CrewMember, int>("SELECT twin(Rank) AS r FROM this", twins).ToList());

            // Assert
            Assert.Contains("nothing", empty.Message);
            Assert.Contains("TWIN", same.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE round(AVG(Age), 2) > 1", "AVG(Age)")]
        [DataRow("SELECT Species FROM this GROUP BY Species, abs(SUM(Age))", "SUM(Age)")]
        public void AggregatesInsideFunctionsDoNotMentionWhere(string sql, string text)
        {
            // Act
            var query = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, double>(sql).ToList());
            var expression = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<CrewMember>("COUNT(*)"));

            // Assert
            Assert.IsFalse(query.Message.Contains("WHERE", StringComparison.Ordinal), query.Message);
            Assert.IsFalse(expression.Message.Contains("WHERE", StringComparison.Ordinal), expression.Message);
            Assert.AreEqual(text, sql.Substring(query.Start, query.Length));
        }

        [TestMethod]
        [DataRow("SELECT round(AVG(Age), 2) AS r FROM this", "65")]
        [DataRow("SELECT abs(SUM(Age)) AS r FROM this", "390")]
        [DataRow("SELECT round(abs(SUM(Age)) / 4.0, 1) AS r FROM this", "97.5")]
        [DataRow("SELECT abs(SUM(Age)) + 1 AS r FROM this", "391")]
        [DataRow("SELECT COUNT(*) AS n, abs(SUM(Age)) AS r FROM this", "6/390")]
        [DataRow("SELECT Species, round(AVG(Age), 1) AS r FROM this GROUP BY Species ORDER BY Species", "Human/38.3; Kaylon/200; Moclan/45; Xelayan/30")]
        [DataRow("SELECT Species, COUNT(*) AS n FROM this GROUP BY Species HAVING abs(SUM(Age)) > 100 ORDER BY Species", "Human/3; Kaylon/1")]
        public void FunctionsOfAggregatesReadTheGroup(string sql, string expected)
        {
            // Act
            var rows = Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join("; ", rows.Select(r => string.Join("/", r.Values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))))));
        }

        [TestMethod]
        [DataRow("SELECT x.Name FROM this", "x.Name", SqlErrorKind.UnknownName)]
        [DataRow("SELECT Name FROM this ORDER BY 3", "3", SqlErrorKind.Semantic)]
        [DataRow("SELECT abs(Rank, 2) AS r FROM this", "abs(Rank, 2)", SqlErrorKind.Semantic)]
        [DataRow("SELECT Name FROM this WHERE abs(Rank, 2) > 1", "abs(Rank, 2)", SqlErrorKind.Semantic)]
        [DataRow("SELECT Name, Species AS Name FROM this", "Species AS Name", SqlErrorKind.Semantic)]
        [DataRow("SELECT Species, Rank, COUNT(*) FROM this GROUP BY Species", "Rank", SqlErrorKind.Semantic)]
        [DataRow("SELECT COUNT(*) AS n, abs(Rank) AS a FROM this", "SELECT COUNT(*) AS n, abs(Rank) AS a FROM this", SqlErrorKind.Semantic)]
        [DataRow("SELECT Name FROM this WHERE Name LIKE 'a%' ESCAPE '!!'", "'!!'", SqlErrorKind.Semantic)]
        [DataRow("SELECT Name FROM this WHERE Name LIKE 'a!' ESCAPE '!'", "'a!'", SqlErrorKind.Semantic)]
        [DataRow("SELECT SUM(Name) FROM this", "SUM(Name)", SqlErrorKind.Semantic)]
        [DataRow("SELECT Name FROM this WHERE Rank = (SELECT Rank FROM this)", "(SELECT Rank FROM this)", SqlErrorKind.Evaluation)]
        public void CompileAndRunErrorsCarryTheirPosition(string sql, string text, SqlErrorKind kind)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql).ToList());

            // Assert
            Assert.AreEqual(kind, e.Kind);
            Assert.AreEqual(text, sql.Substring(e.Start, e.Length));
        }

        [TestMethod]
        public void UnionNamingErrorsCarryTheirPosition()
        {
            // Arrange
            const string Sql = "SELECT Name, Rank FROM crew UNION SELECT Name, Id AS Number FROM ships";

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(Sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.Semantic, e.Kind);
            Assert.AreEqual("SELECT Name, Id AS Number FROM ships", Sql.Substring(e.Start, e.Length));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE (Rank > 1", "(Rank > 1", SqlErrorKind.UnbalancedParentheses)]
        [DataRow("SELECT Name FROM this WHERE Rank > 1)", ")", SqlErrorKind.UnbalancedParentheses)]
        [DataRow("SELECT Name FROM this WHERE (abs(Rank) > 1", "(abs(Rank) > 1", SqlErrorKind.UnbalancedParentheses)]
        [DataRow("SELECT Name FROM this /* open", "/* open", SqlErrorKind.UnterminatedComment)]
        [DataRow("SELECT abs(Rank,'km) AS r FROM this", "'", SqlErrorKind.UnterminatedString)]
        public void UnclosedTextCarriesItsKindAndPosition(string sql, string text, SqlErrorKind kind)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, string>(sql).ToList());

            // Assert
            Assert.AreEqual(kind, e.Kind);
            Assert.AreEqual(text, sql.Substring(e.Start, e.Length));
        }

        [TestMethod]
        public void SyntaxErrorsQuoteTheQueryText()
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, string>("SELECT abs(Rank) AS r FROM this WHERE").ToList());

            // Assert
            Assert.IsFalse(e.Message.Contains("$sqlfn", StringComparison.Ordinal), e.Message);
            Assert.Contains("abs(Rank)", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE Agee > 20", typeof(KeyNotFoundException))]
        [DataRow("SELECT Name FROM this WHERE Age * 1000000000 > 0", typeof(OverflowException))]
        [DataRow("SELECT Name FROM this WHERE Age > 'abc'", typeof(InvalidCastException))]
        [DataRow("SELECT Name FROM this WHERE Name > ANY (SELECT Rank FROM this)", typeof(InvalidOperationException))]
        public void WhereErrorsOnDictionaryRowsPropagate(string sql, Type expected)
        {
            // Act
            var e = Assert.Throws<Exception>(() => Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql).ToList());

            // Assert
            Assert.AreEqual(expected, e.GetType());
        }

        [TestMethod]
        public void WhereErrorsFromFunctionsPropagate()
        {
            // Arrange
            var fail = new TestFunction("fail", arguments => Expression.Throw(Expression.Constant(new InvalidOperationException("fail")), typeof(object)));

            // Act
            var e = Assert.ThrowsExactly<InvalidOperationException>(() => Orville.Crew().Query<CrewMember, string>("SELECT Name FROM this WHERE fail(Rank) > 1", new[] { fail }).ToList());

            // Assert
            Assert.AreEqual("fail", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE ShipId <> 1", "Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE ShipId > 1", "Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE ShipId = 2", "Talla Keyali")]
        public void NullComparesTheSameOnTypedAndDictionaryRows(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, string>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE ShipId = NULL OR Rank = 3", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE NOT (ShipId = 1)", "Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE NOT (ShipId > 1) AND Rank < 3", "Gordon Malloy")]
        [DataRow("SELECT Name FROM this WHERE NOT (ShipId BETWEEN 2 AND 3) AND Rank < 3", "Gordon Malloy")]
        [DataRow("SELECT Name FROM this WHERE ShipId IN (NULL)", "")]
        [DataRow("SELECT Name FROM this WHERE ShipId IN (NULL, NULL)", "")]
        [DataRow("SELECT Name FROM this WHERE ShipId IN (2, NULL)", "Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE ShipId NOT IN (2, NULL)", "")]
        [DataRow("SELECT Name FROM this WHERE ShipId NOT IN (2) AND Rank < 3", "Gordon Malloy")]
        [DataRow("SELECT Name FROM this WHERE 1 IN (ShipId, 2) AND Rank < 3", "Gordon Malloy")]
        [DataRow("SELECT Name FROM this WHERE 1 NOT IN (ShipId, 2)", "Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE ShipId IN (SELECT ShipId FROM this WHERE Rank = 2) AND Rank < 3", "Gordon Malloy")]
        [DataRow("SELECT Name FROM this WHERE ShipId NOT IN (SELECT ShipId FROM this WHERE Rank = 2)", "")]
        [DataRow("SELECT Name FROM this WHERE NOT (ShipId > ALL (SELECT ShipId FROM this WHERE Rank = 5)) AND Rank < 3", "Gordon Malloy")]
        [DataRow("SELECT Name FROM this WHERE NOT (ShipId = ANY (SELECT ShipId FROM this WHERE Rank = 2))", "")]
        public void NullMakesComparisonsUnknownOnTypedAndDictionaryRows(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, string>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name, ShipId <> 1 AS Away FROM this WHERE Rank = 2", "Gordon Malloy/False; Isaac/")]
        [DataRow("SELECT Name, ShipId IN (1, NULL) AS Aboard FROM this WHERE Rank = 2", "Gordon Malloy/True; Isaac/")]
        [DataRow("SELECT Name, NOT (Name LIKE 'I%' AND ShipId = 1) AS Other FROM this WHERE Rank = 2", "Gordon Malloy/True; Isaac/")]
        public void UnknownConditionsSelectNull(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql).Select(r => r.Values.First() + "/" + r.Values.Last());
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, IDictionary<string, object>>(sql).Select(r => r.Values.First() + "/" + r.Values.Last());

            // Assert
            Assert.AreEqual(expected, string.Join("; ", typed));
            Assert.AreEqual(expected, string.Join("; ", dictionaries));
        }

        [TestMethod]
        public void JoinOnNullKeysMatchesNothing()
        {
            // Arrange
            var tables = Orville.DictionaryTables();
            tables["ships"] = new List<IDictionary<string, object>> { new Dictionary<string, object> { ["Id"] = null, ["Name"] = "Unnamed" } };

            // Act
            var result = tables.Query<string>("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id");

            // Assert
            Assert.IsEmpty(result);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this ORDER BY Name", false)]
        [DataRow("SELECT Name FROM this ORDER BY Name DESC", true)]
        [DataRow("SELECT Name AS n FROM this GROUP BY Name ORDER BY n", false)]
        [DataRow("SELECT Name FROM this UNION SELECT Name FROM this ORDER BY Name", false)]
        public void StringsOrderAsLinqOrderByOrdersThem(string sql, bool descending)
        {
            // Arrange
            var ships = new List<Ship> { new Ship { Name = "cherry" }, new Ship { Name = "Banana" }, new Ship { Name = "apple" } };
            var dictionaries = ships.Select(s => (IDictionary<string, object>)new Dictionary<string, object> { ["Name"] = s.Name }).ToList();
            var names = ships.Select(s => s.Name).ToList();
            var expected = string.Join(", ", descending ? names.OrderByDescending(n => n) : names.OrderBy(n => n));

            // Act
            var typed = ships.Query<Ship, string>(sql).ToList();
            var untyped = dictionaries.Query<IDictionary<string, object>, string>(sql).ToList();

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", untyped));
        }

        [TestMethod]
        public void WholeNumbersOfMixedTypesInOneDictionaryColumnCompare()
        {
            // Arrange
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["V"] = 2L },
                new Dictionary<string, object> { ["V"] = 1 },
                new Dictionary<string, object> { ["V"] = 3 },
            };

            // Act
            var ordered = rows.Query<IDictionary<string, object>, object>("SELECT V FROM this ORDER BY V DESC");
            var max = rows.QueryScalar<IDictionary<string, object>, object>("SELECT MAX(V) FROM this");

            // Assert
            Assert.AreEqual("3, 2, 1", string.Join(", ", ordered));
            Assert.AreEqual(3, max);
        }

        [TestMethod]
        [DataRow("SELECT N FROM this WHERE NOT Active", "2")]
        [DataRow("SELECT N FROM this WHERE Born BETWEEN #1/1/2000# AND #12/31/2000#", "1")]
        [DataRow("SELECT N FROM this WHERE #1/1/2001# < Born", "2")]
        [DataRow("SELECT N FROM this WHERE #6/1/2000# = Born", "1")]
        public void BooleansAndDatesCompareOnDictionaryRows(string sql, string expected)
        {
            // Arrange
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["N"] = 1, ["Active"] = true, ["Born"] = new DateTime(2000, 6, 1) },
                new Dictionary<string, object> { ["N"] = 2, ["Active"] = false, ["Born"] = new DateTime(2001, 6, 1) },
                new Dictionary<string, object> { ["N"] = 3, ["Active"] = null, ["Born"] = null },
            };

            // Act
            var result = rows.Query<IDictionary<string, object>, int>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id", "Name", "Name, s.Name")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE Name = 'Orville'", "Name", "Name = 'Orville'")]
        [DataRow("SELECT COUNT(*) AS n FROM crew c, ships s WHERE c.ShipId = s.Id GROUP BY Name", "Name", "Name")]
        [DataRow("SELECT a.Name FROM crew a JOIN crew b ON a.Rank = b.Rank WHERE Species = 'Human'", "Species", "Species")]
        [DataRow("SELECT a.Name FROM crew a JOIN crew b ON Rank = b.Rank", "Rank", "Rank = b.Rank")]
        public void NamesInTwoJoinedTablesAreAmbiguous(string sql, string column, string at)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.Semantic, e.Kind);
            Assert.AreEqual(sql.LastIndexOf(at, StringComparison.Ordinal), e.Start);
            Assert.AreEqual(column.Length, e.Length);
            Assert.AreEqual(column + " can't be resolved because more than one joined table has it.", e.Message);
        }

        [TestMethod]
        public void NamesInThisAndThatAreAmbiguous()
        {
            // Arrange
            const string Sql = "SELECT Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id";

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>(Sql, Orville.Ships()).ToList());
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, IDictionary<string, object>, Tuple<string, string>>(Sql, Orville.ShipDictionaries());

            // Assert
            Assert.AreEqual(SqlErrorKind.Semantic, e.Kind);
            Assert.AreEqual(7, e.Start);
            Assert.AreEqual(4, e.Length);
            Assert.AreEqual("Ed Mercer/Orville; Kelly Grayson/Orville; Bortus/Orville; Gordon Malloy/Orville; Talla Keyali/Roosevelt", Orville.Rows(dictionaries));
        }

        [TestMethod]
        public void PropertiesThatDifferOnlyInCaseReadTheirExactName()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { Name = "upper", name = "lower" } } };

            // Act
            var row = tables.Query("SELECT Name AS a, name AS b FROM this WHERE Name = 'upper' AND name = 'lower'").Single();

            // Assert
            Assert.AreEqual("upper", row["a"]);
            Assert.AreEqual("lower", row["b"]);
        }

        [TestMethod]
        [DataRow("abs(Rank)", typeof(int))]
        [DataRow("abs(ShipId)", typeof(int?))]
        [DataRow("mod(Age, 7)", typeof(int))]
        [DataRow("round(Pay, 1)", typeof(double))]
        [DataRow("power(Rank, 2)", typeof(double))]
        public void MathFunctionsOfTypedValuesStayTyped(string expression, Type expected)
        {
            // Act
            var lambda = SqlExpression.Lambda<CrewMember>((SyntaxTree.Expressions.ExpressionNode)SqlSyntaxTree.ParseExpression(expression).Root);

            // Assert
            Assert.AreEqual(expected, ((UnaryExpression)lambda.Body).Operand.Type);
        }

        [TestMethod]
        public void MathFunctionsOfTypedTextFailToCompile()
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<CrewMember>("abs(Name)"));

            // Assert
            Assert.AreEqual("Arithmetic can't use String values because they aren't numbers.", e.Message);
        }

        [TestMethod]
        public void SignOfNaNIsNaN()
        {
            // Act
            var result = SqlExpression.Compile<CrewMember>("sign(sqrt(-1))")(new CrewMember());

            // Assert
            Assert.AreEqual(double.NaN, result);
        }

        [TestMethod]
        public void SumsOfSmallIntegersStayInLinq()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { Small = (short)1 }, new { Small = (short)2 } } };

            // Act
            var tree = SourcesExtensions.Tree<int>(tables, "SELECT SUM(Small) FROM this");

            // Assert
            Assert.AreEqual(3, tree.Compile()().Single());
            Assert.AreEqual("", TreeShape.Disallowed(tree));
        }

        [TestMethod]
        public void SumOfTextFailsToCompile()
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => new List<CrewMember>().Query<CrewMember, object>("SELECT SUM(Name) FROM this").ToList());

            // Assert
            Assert.AreEqual("Arithmetic can't use String values because they aren't numbers.", e.Message);
        }

        [TestMethod]
        public void DistinctOfOneObjectColumnKeepsEachValue()
        {
            // Act
            var objects = Orville.Crew().Query<CrewMember, object>("SELECT DISTINCT Species FROM this");
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, object>("SELECT DISTINCT Rank FROM this");

            // Assert
            Assert.AreEqual("Human, Moclan, Xelayan, Kaylon", string.Join(", ", objects));
            Assert.AreEqual("5, 4, 3, 2", string.Join(", ", dictionaries));
        }

        [TestMethod]
        public void DistinctTreatsEqualNumbersAsOneValue()
        {
            // Arrange
            var rows = new List<IDictionary<string, object>> { new Dictionary<string, object> { ["A"] = 1 }, new Dictionary<string, object> { ["A"] = 1L } };

            // Act
            var distinct = rows.Query("SELECT DISTINCT A FROM this").Count();
            var union = rows.Query("SELECT A FROM this UNION SELECT A FROM this").Count();

            // Assert
            Assert.AreEqual(1, distinct);
            Assert.AreEqual(1, union);
        }

        [TestMethod]
        public void DictionaryComparerHashesKeysAsItComparesThem()
        {
            // Arrange
            var comparer = new DictionaryComparer<string, object>();
            var upper = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Name"] = 1 };
            var lower = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["name"] = 1L };

            // Act
            var equal = comparer.Equals(upper, lower);

            // Assert
            Assert.IsTrue(equal);
            Assert.AreEqual(comparer.GetHashCode(upper), comparer.GetHashCode(lower));
        }

        [TestMethod]
        public void DynamicRowsReadEachRow()
        {
            // Arrange
            var dictionaries = new List<object> { new Dictionary<string, object> { ["Name"] = "a" }, new Dictionary<string, object> { ["Name"] = "b" } };
            var objects = new List<object> { new Ship { Name = "c" }, new Ship { Name = "d" } };

            // Act
            var fromDictionaries = dictionaries.Query<object, string>("SELECT Name FROM this");
            var fromObjects = objects.Query<object, string>("SELECT Name FROM this");

            // Assert
            Assert.AreEqual("a, b", string.Join(", ", fromDictionaries));
            Assert.AreEqual("c, d", string.Join(", ", fromObjects));
        }

        [TestMethod]
        [DataRow("SELECT Big FROM this WHERE Big > -1")]
        [DataRow("SELECT Big FROM this WHERE Big > Signed")]
        public void UnsignedLongsCompareWithSignedNumbers(string sql)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = new[] { new { Big = ulong.MaxValue, Signed = 1L } } };

            // Act
            var result = tables.Query<ulong>(sql).ToList();

            // Assert
            CollectionAssert.AreEqual(new[] { ulong.MaxValue }, result);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this -- abs(Rank")]
        [DataRow("SELECT Name FROM this /* abs(Rank */")]
        [DataRow("SELECT /* abs(foo bar) */ Name FROM this")]
        public void FunctionCallsInCommentsAreIgnored(string sql)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, string>(sql).ToList();

            // Assert
            Assert.HasCount(6, result);
        }

        [TestMethod]
        public void QualifiedFunctionCallsReportTheirText()
        {
            // Act
            var e = Assert.Throws<SqlException>(() => Orville.Crew().Query("SELECT t.abs(Rank) FROM this t").ToList());

            // Assert
            Assert.DoesNotContain("$sqlfn", e.Message);
            Assert.Contains("t.abs", e.Message);
        }

        [TestMethod]
        [DataRow("_", "[a_b]", 1)]
        [DataRow(".", "[a_b]", 0)]
        [DataRow("%", "[a%b]", 1)]
        [DataRow("%", "[^%]", 0)]
        [DataRow(".", "[^%]", 1)]
        [DataRow("x%y", "x[%]%", 1)]
        public void LikeCharacterClassesReadWildcardsLiterally(string value, string pattern, int expected)
        {
            // Arrange
            var rows = new[] { new Ship { Name = value } };

            // Act
            var result = rows.Query("SELECT * FROM this WHERE Name LIKE '" + pattern + "'").Count();

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("HAVING Name = 'Ed Mercer'", "Name")]
        [DataRow("HAVING abs(Rank) > 3", "Rank")]
        public void HavingCantReadUngroupedFields(string having, string field)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, IDictionary<string, object>>("SELECT Species, COUNT(*) FROM this GROUP BY Species " + having).ToList());

            // Assert
            Assert.AreEqual("The field '" + field + "' can't be used in HAVING because it isn't in the GROUP BY clause.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Rank, COUNT(*) AS n FROM this GROUP BY Rank HAVING abs(Rank) > 3", "5/1; 4/1")]
        [DataRow("SELECT mod(Rank, 2) AS r, COUNT(*) AS n FROM this GROUP BY mod(Rank, 2) HAVING mod(Rank, 2) = 0", "0/3")]
        [DataRow("SELECT mod(Rank, 2) AS r, COUNT(*) AS n FROM this GROUP BY mod(Rank, 2) HAVING abs(mod(Rank, 2)) = 0", "0/3")]
        public void HavingReadsGroupedFieldsInFunctions(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Tuple<int, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Name, (SELECT COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id) AS N FROM ships")]
        [DataRow("SELECT Name FROM ships WHERE Id < (SELECT COUNT(*) FROM crew c, ships s WHERE c.ShipId = s.Id)")]
        public void SubqueriesCantJoinSources(string sql)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());

            // Assert
            Assert.AreEqual("The subquery can't join its sources because a subquery reads only one source.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT abs((SELECT COUNT(*) FROM ships)) AS N FROM crew WHERE Rank > 3", "3, 3")]
        [DataRow("SELECT Rank FROM crew WHERE Rank = abs((SELECT COUNT(*) FROM ships))", "3, 3")]
        [DataRow("SELECT Rank FROM crew c WHERE Rank = abs((SELECT COUNT(*) FROM ships s WHERE s.Id >= c.ShipId))", "3")]
        [DataRow("SELECT abs((SELECT COUNT(*) FROM crew WHERE Rank > 3)) AS N FROM crew GROUP BY Species", "2, 2, 2, 2")]
        [DataRow("SELECT COUNT(*) + abs((SELECT COUNT(*) FROM crew WHERE Rank > 3)) AS N FROM crew", "8")]
        public void SubqueriesInFunctionArgumentsReadTheirOwnSource(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<int>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("Rank > (SELECT MIN(Rank) FROM this)")]
        [DataRow("abs((SELECT MIN(Rank) FROM this))")]
        public void ExpressionsCantEvaluateSubqueries(string expression)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<CrewMember>(expression));

            // Assert
            Assert.AreEqual("The subquery can't be evaluated because the expression has no table for it to read.", e.Message);
        }

        [TestMethod]
        public void FindingAChildPastTheLastIsNull()
        {
            // Arrange
            var root = SqlSyntaxTree.Parse("SELECT * FROM this").Root;

            // Act
            var child = root.FindChild<SyntaxTree.SyntaxNode>(root.Children.Count);

            // Assert
            Assert.IsNull(child);
        }

        [TestMethod]
        [DataRow("SELECT N FROM this WHERE Active AND Flag", "1")]
        [DataRow("SELECT N FROM this WHERE Active OR Flag", "1, 2, 4")]
        [DataRow("SELECT N FROM this WHERE NOT (Active AND Flag)", "2, 3")]
        [DataRow("SELECT N FROM this WHERE RPM AND Active", "1")]
        [DataRow("SELECT N FROM this WHERE RPM OR Flag", "1, 4, 5")]
        [DataRow("SELECT N FROM this WHERE RPM AND N > 1", "4, 5")]
        [DataRow("SELECT N FROM this WHERE NOT RPM", "2")]
        public void DictionaryValuesAreConditionsInAndOrAndNot(string sql, string expected)
        {
            // Arrange
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["N"] = 1, ["Active"] = true, ["Flag"] = true, ["RPM"] = 3000 },
                new Dictionary<string, object> { ["N"] = 2, ["Active"] = true, ["Flag"] = false, ["RPM"] = 0 },
                new Dictionary<string, object> { ["N"] = 3, ["Active"] = false, ["Flag"] = null, ["RPM"] = null },
                new Dictionary<string, object> { ["N"] = 4, ["Active"] = null, ["Flag"] = true, ["RPM"] = 2.5 },
                new Dictionary<string, object> { ["N"] = 5, ["Active"] = null, ["Flag"] = null, ["RPM"] = 1 },
            };

            // Act
            var result = rows.Query<IDictionary<string, object>, int>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE NOT Moons", "Moclus")]
        [DataRow("SELECT Name FROM this WHERE Moons AND Gravity > 1", "Xelaya, Kaylon 1")]
        [DataRow("SELECT Name FROM this WHERE Moons OR Gravity > 3", "Earth, Xelaya, Kaylon 1")]
        public void TypedNumbersAreConditionsInAndOrAndNot(string sql, string expected)
        {
            // Act
            var typed = Orville.Planets().Query<Planet, string>(sql);
            var dictionaries = Orville.PlanetDictionaries().Query<IDictionary<string, object>, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT d.C FROM t, d WHERE t.C = d.C")]
        [DataRow("SELECT d.C FROM t, d WHERE d.C = t.C")]
        [DataRow("SELECT d.D FROM t, d WHERE t.D = d.D")]
        [DataRow("SELECT d.D FROM t, d WHERE d.D = t.D")]
        [DataRow("SELECT d.E FROM t, d WHERE t.E = d.E")]
        [DataRow("SELECT d.E FROM t, d WHERE d.E = t.E")]
        public void TypedAndDictionaryValuesCompareInEitherOrder(string sql)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable>
            {
                ["t"] = new[] { new { C = 'a', D = 1.0000000000000001m, E = DayOfWeek.Monday } },
                ["d"] = new List<IDictionary<string, object>> { new Dictionary<string, object> { ["C"] = 'a', ["D"] = 1.0000000000000001m, ["E"] = DayOfWeek.Monday } },
            };

            // Act
            var result = tables.Query(sql).Count();

            // Assert
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        [DataRow("SELECT Id FROM this WHERE Id IN (Numbers)", "1, 3")]
        [DataRow("SELECT Id FROM this WHERE Id NOT IN (Numbers)", "2, 4")]
        [DataRow("SELECT Id FROM this WHERE Name IN (Tags)", "1")]
        [DataRow("SELECT Id FROM this WHERE Name NOT IN (Tags)", "4")]
        public void InAnArrayColumnMatchesItsElements(string sql, string expected)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable>
            {
                ["this"] = new[]
                {
                    new { Id = 1, Name = "a", Numbers = new[] { 1, 2 }, Tags = new[] { "a", "b" } },
                    new { Id = 2, Name = "b", Numbers = new[] { 3 }, Tags = new[] { "a", null } },
                    new { Id = 3, Name = (string)null, Numbers = new[] { 3 }, Tags = new[] { "c" } },
                    new { Id = 4, Name = "d", Numbers = Array.Empty<int>(), Tags = Array.Empty<string>() },
                },
            };

            // Act
            var result = tables.Query<int>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        public void ANullConditionInANonNullableMemberSaysSo()
        {
            // Arrange
            const string Sql = "SELECT Name, ShipId > 1 AS Flag FROM this";

            // Act
            var member = Assert.ThrowsExactly<InvalidOperationException>(() => Orville.Crew().Query<CrewMember, Flagged>(Sql).ToList());
            var tuple = Assert.ThrowsExactly<InvalidOperationException>(() => Orville.Crew().Query<CrewMember, Tuple<string, bool>>(Sql).ToList());

            // Assert
            Assert.AreEqual("The query returned NULL, which Boolean can't hold.", member.Message);
            Assert.AreEqual(member.Message, tuple.Message);
        }

        [TestMethod]
        public void ANullRowOfAnUntypedSourceSaysItIsNull()
        {
            // Arrange
            IDictionary<string, object> row = new System.Dynamic.ExpandoObject();
            row["X"] = 2;
            var rows = new List<object> { row, null };

            // Act
            var e = Assert.ThrowsExactly<InvalidOperationException>(() => rows.Query("SELECT * FROM this WHERE X > 1").ToList());

            // Assert
            Assert.AreEqual("X can't be read because the row is null.", e.Message);
        }

        [TestMethod]
        public void AnUnknownFieldOfAnUntypedSourceIsAnUnknownName()
        {
            // Arrange
            var rows = new List<object> { new Ship { Name = "Orville" } };

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => rows.Query<object, string>("SELECT Warp FROM this").ToList());

            // Assert
            Assert.AreEqual(SqlErrorKind.UnknownName, e.Kind);
            Assert.AreEqual("Warp can't be read because it isn't a field of Ship.", e.Message);
        }

        private sealed class Ranked
        {
            public int Rank { get; set; }

            public static implicit operator Ranked(int rank)
            {
                return new Ranked { Rank = rank * 10 };
            }
        }

        private sealed class Flagged
        {
            public string Name { get; set; }

            public bool Flag { get; set; }
        }
    }
}
