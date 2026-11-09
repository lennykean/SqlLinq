using System;
using System.Collections;
using System.Collections.Generic;
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
        [DataRow("SELECT x.Name FROM crew c", "x in x.Name isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c ORDER BY x.Name", "x in x.Name isn't a source or alias of this query")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING x.Species = 'Human'", "x in x.Species isn't a source or alias of this query")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING MAX(x.Rank) > 3", "x in x.Rank isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c WHERE x.Rank > 3", "x in x.Rank isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE x.Rank > 3", "x in x.Rank isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY x.Name", "x in x.Name isn't a source or alias of this query")]
        [DataRow("SELECT x.Name FROM crew c, ships s WHERE c.ShipId = s.Id", "x in x.Name isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank > (SELECT MAX(x.Rank) FROM crew c2)", "x in x.Rank isn't a source or alias of this query")]
        public void UnknownAliasesThrowSqlExceptionEverywhere(string sql, string message)
        {
            // Act
            var typed = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            var dictionaries = Assert.ThrowsExactly<SqlException>(() => Orville.DictionaryTables().Query(sql).ToList());

            // Assert
            Assert.Contains(message, typed.Message);
            Assert.Contains(message, dictionaries.Message);
        }
    }
}
