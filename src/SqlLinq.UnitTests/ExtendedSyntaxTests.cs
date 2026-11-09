using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class ExtendedSyntaxTests
    {
        private static readonly ISqlFunction[] Twice =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
        ];

        private static Dictionary<string, IEnumerable> Tables()
        {
            return new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.Crew(),
                ["ships"] = Orville.Ships(),
            };
        }

        private static List<Ship> MarkedShips()
        {
            return
            [
                new Ship { Id = 1, Name = "Orville", Class = "Exploratory" },
                new Ship { Id = 2, Name = "Orville_2", Class = "Shuttle" },
                new Ship { Id = 3, Name = "OrvilleX2", Class = "Shuttle" },
                new Ship { Id = 4, Name = "Shields 100%", Class = "Status" },
                new Ship { Id = 5, Name = "Shields 100", Class = "Status" },
                new Ship { Id = 6, Name = "Deck!5", Class = "Location" },
            ];
        }

        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew AS c WHERE c.Rank > 3", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew AS c JOIN ships AS s ON c.ShipId = s.Id WHERE s.Id = 2", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew AS c, ships AS s WHERE c.ShipId = s.Id AND s.Id = 2", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships AS s ON c.ShipId = s.Id WHERE c.Rank = 5", "Ed Mercer/Orville")]
        [DataRow("SELECT c.Name, c.Species FROM crew AS c WHERE c.Rank > (SELECT AVG(k.Rank) FROM crew AS k WHERE k.Species = c.Species)", "Ed Mercer/Human; Kelly Grayson/Human")]
        public void FromAcceptsAsBeforeAnAlias(string sql, string expected)
        {
            // Act
            var result = Tables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void SingleTableQueryAcceptsAsBeforeAnAlias()
        {
            // Act
            var result = Orville.Crew().Query("SELECT * FROM this AS c WHERE c.Rank > 3").Select(c => c.Name);

            // Assert
            Assert.AreEqual("Ed Mercer, Kelly Grayson", string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew ORDER BY 2 DESC, 1", "Talla Keyali/Xelayan; Bortus/Moclan; Isaac/Kaylon; Ed Mercer/Human; Gordon Malloy/Human; Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank > 2 ORDER BY 2, 1 DESC", "Kelly Grayson/Human; Ed Mercer/Human; Bortus/Moclan; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank < 4 ORDER BY Species DESC, 1", "Talla Keyali/Xelayan; Bortus/Moclan; Isaac/Kaylon; Gordon Malloy/Human")]
        [DataRow("SELECT Name AS Officer, Species AS Kind FROM crew WHERE Rank > 3 ORDER BY 1 DESC", "Kelly Grayson/Human; Ed Mercer/Human")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY 2 DESC, 1", "Talla Keyali/Roosevelt; Bortus/Orville; Ed Mercer/Orville; Gordon Malloy/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT DISTINCT c.Species, s.Class FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY 1 DESC", "Xelayan/Exploratory; Moclan/Exploratory; Human/Exploratory")]
        public void OrderByPositionSortsBySelectListColumn(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank IN (5, NULL)", "Ed Mercer")]
        [DataRow("SELECT * FROM this WHERE Name IN (NULL, 'Bortus', 'Isaac')", "Bortus, Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay IN (2, 3, 4.5)", "Kelly Grayson, Bortus, Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay IN (Rank, 2.5)", "Bortus, Gordon Malloy, Isaac")]
        [DataRow("SELECT * FROM this WHERE Name BETWEEN 'Bortus' AND 'Ed Mercer'", "Ed Mercer, Bortus")]
        [DataRow("SELECT * FROM this WHERE Name < 'b'", "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali, Isaac")]
        public void InListsAndStringOrderingAcrossRowShapes(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query(sql).Select(c => c.Name);
            var dictionaries = Orville.CrewDictionaries().Query(sql).Select(c => c["Name"]);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM this GROUP BY Species ORDER BY 1 DESC", "Xelayan/1; Moclan/1; Kaylon/1; Human/3")]
        [DataRow("SELECT this.Species, MAX(Rank) FROM this WHERE Rank > 2 GROUP BY this.Species ORDER BY 1", "Human/5; Moclan/3; Xelayan/3")]
        public void OrderByPositionSortsGroups(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, System.Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species ORDER BY 2 DESC, 1", "Human/3; Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) AS Total FROM crew GROUP BY Species ORDER BY Total, Species DESC", "Xelayan/1; Moclan/1; Kaylon/1; Human/3")]
        [DataRow("SELECT Species, MAX(Age) - MIN(Age) AS Span FROM crew GROUP BY Species ORDER BY Span DESC, Species", "Human/7; Kaylon/0; Moclan/0; Xelayan/0")]
        [DataRow("SELECT Species, MAX(Age) - MIN(Age) AS Span FROM crew GROUP BY Species ORDER BY 2, 1 DESC", "Xelayan/0; Moclan/0; Kaylon/0; Human/7")]
        [DataRow("SELECT Name, Rank * 2 AS Twice FROM crew WHERE Rank < 4 ORDER BY 2 DESC, Name", "Bortus/6; Talla Keyali/6; Gordon Malloy/4; Isaac/4")]
        [DataRow("SELECT Name, Rank * 2 AS Twice FROM crew WHERE Rank < 4 ORDER BY Twice, Name", "Gordon Malloy/4; Isaac/4; Bortus/6; Talla Keyali/6")]
        [DataRow("SELECT s.Name AS Ship, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name ORDER BY 2, Ship", "Roosevelt/1; Orville/4")]
        public void OrderBySortsByAggregateAndComputedColumns(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, int>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this ORDER BY 3 DESC, 1", "Ed Mercer, Kelly Grayson, Bortus, Talla Keyali, Gordon Malloy, Isaac")]
        [DataRow("SELECT * FROM this WHERE Rank < 5 ORDER BY 2, 1", "Gordon Malloy, Kelly Grayson, Isaac, Bortus, Talla Keyali")]
        public void OrderByPositionOnStarUsesTheTypesColumnOrder(string sql, string expected)
        {
            // Act
            var viaQuery = Orville.Crew().Query(sql).Select(c => c.Name);
            var viaTables = Orville.Tables().Query<CrewMember>(sql.Replace("FROM this", "FROM crew")).Select(c => c.Name);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", viaQuery));
            Assert.AreEqual(expected, string.Join(", ", viaTables));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew ORDER BY 0", "ORDER BY 0 is out of range; the select list has 2 columns.")]
        [DataRow("SELECT Name, Species FROM crew ORDER BY 1, 3", "ORDER BY 3 is out of range; the select list has 2 columns.")]
        [DataRow("SELECT DISTINCT Name, Species FROM crew ORDER BY 3", "ORDER BY 3 is out of range; the select list has 2 columns.")]
        [DataRow("SELECT * FROM crew ORDER BY 7", "ORDER BY 7 is out of range; the select list has 6 columns.")]
        [DataRow("SELECT * FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY 1", "ORDER BY 1 needs a select list; SELECT * over dictionary or dynamic rows has no fixed column order.")]
        public void OrderByPositionOutsideTheSelectListThrows(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.AreEqual(message, e.Message);
        }

        [TestMethod]
        public void OrderByPositionOnStarOverDictionaryRowsThrows()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.DictionaryTables().Query("SELECT * FROM crew ORDER BY 1").ToList());
            Assert.AreEqual("ORDER BY 1 needs a select list; SELECT * over dictionary or dynamic rows has no fixed column order.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE 'Orville_%'", "Orville_2, OrvilleX2")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE 'Orville!_%' ESCAPE '!'", "Orville_2")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE 'orville\\_%' ESCAPE '\\'", "Orville_2")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE '%100!%' ESCAPE '!'", "Shields 100%")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE '%!!%' ESCAPE '!'", "Deck!5")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE 'Orvill!e%' ESCAPE '!'", "Orville, Orville_2, OrvilleX2")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE '%[!%]' ESCAPE '!'", "Shields 100%")]
        [DataRow("SELECT Name FROM ships WHERE Name NOT LIKE '%!%' ESCAPE '!'", "Orville, Orville_2, OrvilleX2, Shields 100, Deck!5")]
        [DataRow("SELECT s.Name FROM ships s WHERE s.Name NOT LIKE 'Orville#_%' ESCAPE '#' AND s.Class = 'Shuttle'", "OrvilleX2")]
        public void LikeEscapeMakesTheNextCharacterLiteral(string sql, string expected)
        {
            // Arrange
            var ships = MarkedShips();
            var dictionaries = ships
                .Select(s => (IDictionary<string, object>)new Dictionary<string, object> { ["Id"] = s.Id, ["Name"] = s.Name, ["Class"] = s.Class })
                .ToList();

            // Act
            var typed = new Dictionary<string, IEnumerable> { ["ships"] = ships }.Query<string>(sql);
            var viaDictionaries = new Dictionary<string, IEnumerable> { ["ships"] = dictionaries }.Query<string>(sql);
            var viaQuery = ships.Query<Ship, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", viaDictionaries));
            Assert.AreEqual(expected, string.Join(", ", viaQuery));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE 'Orville%' ESCAPE '!!'", "The LIKE escape '!!' must be exactly one character.")]
        [DataRow("SELECT Name FROM ships WHERE Name NOT LIKE 'Orville%' ESCAPE ''", "The LIKE escape '' must be exactly one character.")]
        [DataRow("SELECT Name FROM ships WHERE Name LIKE 'Orville!' ESCAPE '!'", "The LIKE pattern 'Orville!' ends with its escape character '!'.")]
        [DataRow("SELECT Name FROM ships WHERE Name NOT LIKE '%!' ESCAPE '!'", "The LIKE pattern '%!' ends with its escape character '!'.")]
        public void LikeEscapeThatIsNotOneCharacterThrows(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => new Dictionary<string, IEnumerable> { ["ships"] = MarkedShips() }.Query(sql).ToList());
            Assert.AreEqual(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT SUM(DISTINCT Rank), SUM(Rank) FROM crew", "14, 19")]
        [DataRow("SELECT SUM(DISTINCT ShipId), SUM(ShipId) FROM crew", "3, 6")]
        [DataRow("SELECT AVG(DISTINCT ShipId), AVG(ShipId) FROM crew", "1.5, 1.2")]
        [DataRow("SELECT MIN(DISTINCT Age), MAX(DISTINCT Pay) FROM crew WHERE Rank > 2", "30, 5.5")]
        [DataRow("SELECT SUM(DISTINCT Pay) AS S, AVG(DISTINCT Rank) AS A, MIN(DISTINCT Age) AS M, MAX(DISTINCT Age) AS X FROM crew", "21.0, 3.5, 30, 200")]
        [DataRow("SELECT Name FROM crew WHERE ShipId = (SELECT SUM(DISTINCT ShipId) FROM crew) - 1", "Talla Keyali")]
        public void DistinctAggregatesUseEachValueOnce(string sql, string expected)
        {
            // Act
            var viaTables = Orville.Tables().Query(sql).Single();
            var viaQuery = Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql).Single();

            // Assert
            Assert.AreEqual(expected, string.Join(", ", viaTables.Values.Select(v => System.Convert.ToString(v, CultureInfo.InvariantCulture))));
            Assert.AreEqual(expected, string.Join(", ", viaQuery.Values.Select(v => System.Convert.ToString(v, CultureInfo.InvariantCulture))));
        }

        [TestMethod]
        [DataRow("SELECT Species, SUM(DISTINCT ShipId) FROM crew WHERE ShipId IS NOT NULL GROUP BY Species ORDER BY Species", "Human/1; Moclan/1; Xelayan/2")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING SUM(DISTINCT ShipId) < SUM(ShipId) ORDER BY Species", "Human/3")]
        [DataRow("SELECT c.Species, MAX(DISTINCT c.Rank) FROM crew c GROUP BY c.Species ORDER BY 1", "Human/5; Kaylon/2; Moclan/3; Xelayan/3")]
        public void DistinctAggregatesUseEachValueOncePerGroup(string sql, string expected)
        {
            // Act
            var viaTables = Orville.Tables().Query<System.Tuple<string, int>>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, System.Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(viaTables));
            Assert.AreEqual(expected, Orville.Rows(viaQuery));
        }

        [TestMethod]
        [DataRow("SELECT SUM(Value()) FROM this", "19")]
        [DataRow("SELECT SUM(DISTINCT Value()) FROM this", "14")]
        [DataRow("SELECT AVG(DISTINCT Value()) FROM this", "3.5")]
        [DataRow("SELECT MIN(DISTINCT Value()) FROM this", "2")]
        [DataRow("SELECT MAX(DISTINCT Value()) FROM this", "5")]
        public void DistinctAggregatesOverValues(string sql, string expected)
        {
            // Arrange
            var ranks = Orville.Crew().Select(c => c.Rank).ToList();

            // Act
            var result = ranks.QueryScalar<int, object>(sql);

            // Assert
            Assert.AreEqual(expected, System.Convert.ToString(result, CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void CountOfAnExpressionIsNamedByItsField()
        {
            // Act
            var row = Orville.Tables().Query("SELECT COUNT(ShipId), COUNT(ALL ShipId), COUNT(*) FROM crew").Single();
            var dictionaryRow = Orville.DictionaryTables().Query("SELECT COUNT(s.Class) FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id").Single();

            // Assert
            Assert.AreEqual(5, row["CountShipId"]);
            Assert.AreEqual(5, row["CountAllShipId"]);
            Assert.AreEqual(6, row["CountStar"]);
            Assert.AreEqual(5, dictionaryRow["CountClass"]);
        }

        [TestMethod]
        public void DistinctAggregatesAreNamedApartFromPlainOnes()
        {
            // Act
            var row = Orville.Tables().Query("SELECT SUM(DISTINCT Rank), SUM(Rank), MAX(DISTINCT Age) FROM crew").Single();

            // Assert
            Assert.AreEqual(14, row["SumDistinctRank"]);
            Assert.AreEqual(19, row["SumRank"]);
            Assert.AreEqual(200, row["MaxDistinctAge"]);
        }

        [TestMethod]
        [DataRow("SELECT Species, SUM(DISTINCT Rank) FROM crew", "Your query contains fields and aggregates but no GROUP BY clause.")]
        [DataRow("SELECT Name FROM crew WHERE AVG(DISTINCT Rank) > 3", "Aggregate expressions are not valid in a WHERE clause.")]
        [DataRow("SELECT SUM(DISTINCT) FROM crew", "SQL parse error")]
        [DataRow("SELECT MAX(DISTINCT *) FROM crew", "SQL parse error")]
        public void DistinctAggregateMistakesThrow(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name, Pay * 2 AS Twice FROM crew WHERE Rank > 3", "Ed Mercer/11; Kelly Grayson/9")]
        [DataRow("SELECT c.Name, c.Rank * 10 + c.Age AS Score FROM crew c WHERE c.Species = 'Moclan'", "Bortus/75")]
        [DataRow("SELECT Name, -Rank AS Down FROM crew WHERE Rank = 5", "Ed Mercer/-5")]
        [DataRow("SELECT Name, (Rank + 1) * 2 AS Next FROM crew WHERE Rank < 3 ORDER BY Name", "Gordon Malloy/6; Isaac/6")]
        [DataRow("SELECT Name, 1.5 AS Bonus FROM crew WHERE Rank = 4", "Kelly Grayson/1.5")]
        public void ComputedColumnsAreCalculatedPerRow(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, double>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, double>>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, System.Tuple<string, double>>(sql);
            var viaDictionaryQuery = Orville.CrewDictionaries().Query<IDictionary<string, object>, System.Tuple<string, double>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
            Assert.AreEqual(expected, Orville.Rows(viaQuery));
            Assert.AreEqual(expected, Orville.Rows(viaDictionaryQuery));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id) AS Aboard FROM ships s", "Orville/4; Roosevelt/1; Pembroke/0")]
        [DataRow("SELECT c.Name, c.Rank * 100 + s.Id AS Code FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank > 3", "Ed Mercer/501; Kelly Grayson/401")]
        [DataRow("SELECT c.Name, c.Rank + s.Id AS Code FROM crew c, ships s WHERE c.ShipId = s.Id AND s.Id = 2", "Talla Keyali/5")]
        public void ComputedColumnsReadOtherTables(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, double>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, double>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT MAX(Pay) - MIN(Pay) AS Spread FROM crew", "3.5")]
        [DataRow("SELECT COUNT(*) AS N, SUM(Rank) * 2 AS Doubled, MAX(Age) - MIN(Age) AS Span FROM crew", "6, 38, 170")]
        [DataRow("SELECT COUNT(*) + 1 AS N, 'crew' AS Tag FROM crew WHERE Rank > 3", "3, crew")]
        [DataRow("SELECT Species, MAX(Rank) - MIN(Rank) AS Spread, COUNT(*) FROM crew GROUP BY Species ORDER BY Species", "Human, 3, 3; Kaylon, 0, 1; Moclan, 0, 1; Xelayan, 0, 1")]
        [DataRow("SELECT ShipId, ShipId * 10 + COUNT(*) AS Code FROM crew WHERE ShipId IS NOT NULL GROUP BY ShipId ORDER BY ShipId", "1, 14; 2, 21")]
        [DataRow("SELECT Species, MAX(Age) - MIN(Age) AS Span FROM crew GROUP BY Species HAVING Span > 5", "Human, 7")]
        [DataRow("SELECT Name, Rank - (SELECT MIN(Rank) FROM crew) AS AboveLowest FROM crew WHERE Rank > 3", "Ed Mercer, 3; Kelly Grayson, 2")]
        [DataRow("SELECT Name, Rank * 2 AS Twice, Species FROM crew WHERE Rank > 2 ORDER BY 3 DESC, 1", "Talla Keyali, 6, Xelayan; Bortus, 6, Moclan; Ed Mercer, 10, Human; Kelly Grayson, 8, Human")]
        [DataRow("SELECT Name FROM crew WHERE Rank > (SELECT MAX(Rank) - 1 AS Second FROM crew)", "Ed Mercer")]
        public void ComputedColumnsMixWithFieldsAndAggregates(string sql, string expected)
        {
            // Act
            var viaTables = Orville.Tables().Query(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql);

            // Assert
            Assert.AreEqual(expected, Values(viaTables));
            Assert.AreEqual(expected, Values(viaQuery));
        }

        [TestMethod]
        public void ComputedColumnsFillEveryResultShape()
        {
            // Arrange
            const string PerRow = "SELECT Name, Species AS Ship, Rank * 2 AS Rank FROM crew WHERE Name = 'Bortus'";
            const string Grouped = "SELECT Species, MAX(Rank) - MIN(Rank) AS Total FROM crew GROUP BY Species ORDER BY Species";
            var tables = Orville.Tables();

            // Act
            var posting = tables.Query<Posting>(PerRow).Single();
            var tuple = tables.Query<System.Tuple<string, string, int>>(PerRow).Single();
            var dictionary = tables.Query(PerRow).Single();
            var expando = tables.Query<dynamic>(PerRow).Single();
            var scalar = tables.Query<int>("SELECT Rank * 2 AS Twice FROM crew WHERE Name = 'Bortus'").Single();
            var tally = tables.Query<SpeciesTally>(Grouped).First();
            var groupedTuple = tables.Query<System.Tuple<string, int>>(Grouped).First();
            var groupedDictionary = tables.Query(Grouped).First();
            var groupedExpando = tables.Query<dynamic>(Grouped).First();
            var groupedScalar = tables.Query<int>("SELECT MAX(Rank) - MIN(Rank) AS Spread FROM crew").Single();

            // Assert
            Assert.AreEqual("Bortus/Moclan/6", posting.Name + "/" + posting.Ship + "/" + posting.Rank);
            Assert.AreEqual(System.Tuple.Create("Bortus", "Moclan", 6), tuple);
            Assert.AreEqual("Bortus, Moclan, 6", Values([dictionary]));
            Assert.AreEqual(6, expando.Rank);
            Assert.AreEqual("Moclan", expando.Ship);
            Assert.AreEqual(6, scalar);
            Assert.AreEqual("Human/3", tally.Species + "/" + tally.Total);
            Assert.AreEqual(System.Tuple.Create("Human", 3), groupedTuple);
            Assert.AreEqual(3, groupedDictionary["Total"]);
            Assert.AreEqual(3, groupedExpando.Total);
            Assert.AreEqual(3, groupedScalar);
        }

        [TestMethod]
        [DataRow("SELECT Name, Age / 2 AS Half FROM crew WHERE Rank = 3", "Bortus/22; Talla Keyali/15")]
        [DataRow("SELECT Name, (Age + 1) / 2 AS Half FROM crew WHERE Rank = 3", "Bortus/23; Talla Keyali/15")]
        [DataRow("SELECT Name, Age * 2 / 4 AS Half FROM crew WHERE Rank = 3", "Bortus/22; Talla Keyali/15")]
        [DataRow("SELECT Name, Age - Rank * 10 AS Rest FROM crew WHERE Rank = 5", "Ed Mercer/-9")]
        [DataRow("SELECT Name, -Age / 2 AS Half FROM crew WHERE Rank = 3", "Bortus/-22; Talla Keyali/-15")]
        [DataRow("SELECT Name, Rank FROM crew WHERE -Age / 2 = -22", "Bortus/3")]
        public void WholeNumberArithmeticMatchesAcrossRowShapes(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, System.Tuple<string, int>>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, System.Tuple<string, int>>(sql);
            var expandos = Orville.CrewExpandos().Query<dynamic, System.Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
            Assert.AreEqual(expected, Orville.Rows(expandos));
        }

        [TestMethod]
        public void WholeNumberArithmeticOnBoxedRowsStaysWhole()
        {
            // Arrange
            const string Sql = "SELECT Age / 2 AS Half, Age + Rank AS Total FROM crew WHERE Rank = 5";

            // Act
            var typed = Orville.Crew().Query<CrewMember, IDictionary<string, object>>(Sql).Single();
            var dictionary = Orville.CrewDictionaries().Query<IDictionary<string, object>, IDictionary<string, object>>(Sql).Single();

            // Assert
            Assert.AreEqual(20, typed["Half"]);
            Assert.AreEqual(20, dictionary["Half"]);
            Assert.AreEqual(46, dictionary["Total"]);
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) AS N, COUNT(ALL ShipId) AS Posted, SUM(Rank) AS Ranks, AVG(Pay) AS Pay, MAX(Name) AS Last, SUM(Rank) * 2 AS Twice FROM crew WHERE Rank > 10")]
        [DataRow("SELECT COUNT(*) AS N, VAR(Rank) AS V, VARP(Rank) AS VP, STDEV(Age) AS SD, STDEVP(Pay) AS SDP FROM crew WHERE Rank > 10")]
        [DataRow("SELECT COUNT(ALL ShipId) AS Posted, SUM(ShipId) AS S, AVG(ShipId) AS A, MIN(ShipId) AS Lo, MAX(ShipId) AS Hi, VAR(ShipId) AS V, VARP(ShipId) AS VP, STDEV(ShipId) AS SD, STDEVP(ShipId) AS SDP FROM crew WHERE ShipId IS NULL")]
        public void AggregatesOverNoRowsAreNullExceptCount(string sql)
        {
            // Act
            var typed = Orville.Tables().Query(sql).Single();
            var dictionaries = Orville.DictionaryTables().Query(sql).Single();

            // Assert
            foreach (var row in new[] { typed, dictionaries })
            {
                foreach (var column in row)
                {
                    if (column.Key == "N" || column.Key == "Posted")
                    {
                        Assert.AreEqual(0, column.Value, column.Key);
                    }
                    else
                    {
                        Assert.IsNull(column.Value, column.Key);
                    }
                }
            }
        }

        [TestMethod]
        [DataRow("SELECT VAR(Rank) AS V, STDEV(Age) AS SD, VARP(Rank) AS VP, STDEVP(Age) AS SDP FROM crew WHERE Species = 'Moclan'", "NULL, NULL, 0, 0")]
        [DataRow("SELECT VAR(ShipId) AS V, STDEV(ShipId) AS SD, VARP(ShipId) AS VP FROM crew WHERE Species = 'Xelayan' OR ShipId IS NULL", "NULL, NULL, 0")]
        [DataRow("SELECT VAR(Rank) AS V, STDEV(Rank) AS SD FROM crew WHERE Rank = 3", "0, 0")]
        [DataRow("SELECT VAR(Rank) AS V, VARP(Rank) AS VP FROM crew WHERE Name IN ('Gordon Malloy', 'Kelly Grayson')", "2, 1")]
        [DataRow("SELECT Species, VAR(Rank) AS V FROM crew WHERE Species <> 'Human' GROUP BY Species ORDER BY Species", "Kaylon, NULL; Moclan, NULL; Xelayan, NULL")]
        public void SampleVarianceOfOneValueIsNull(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query(sql);
            var dictionaries = Orville.DictionaryTables().Query(sql);

            // Assert
            Assert.AreEqual(expected, Values(typed, "NULL"));
            Assert.AreEqual(expected, Values(dictionaries, "NULL"));
        }

        [TestMethod]
        public void ComputedColumnsOverDynamicRows()
        {
            // Arrange
            IEnumerable<dynamic> crew = Orville.CrewExpandos();

            // Act
            var result = crew.Query<dynamic, dynamic>("SELECT c.Name, c.Rank + 1 AS Next FROM crew c WHERE c.Rank > 3 ORDER BY c.Name").ToList();

            // Assert
            Assert.HasCount(2, result);
            Assert.AreEqual("Ed Mercer", result[0].Name);
            Assert.AreEqual(6d, System.Convert.ToDouble((object)result[0].Next, CultureInfo.InvariantCulture));
            Assert.AreEqual(5d, System.Convert.ToDouble((object)result[1].Next, CultureInfo.InvariantCulture));
        }

        [TestMethod]
        [DataRow("SELECT Name, MAX(Pay) - 1 AS Top FROM crew", "Your query contains fields and aggregates but no GROUP BY clause.")]
        [DataRow("SELECT MAX(Pay) - Pay AS Gap FROM crew", "Your query contains fields and aggregates but no GROUP BY clause.")]
        [DataRow("SELECT Species, MAX(Age) - Rank AS Gap FROM crew GROUP BY Species", "The query contains the field 'Rank' that is not matched between the select list and group by clause.")]
        [DataRow("SELECT Name, Pay * 2 FROM crew", "SQL parse error")]
        public void ComputedColumnMistakesThrow(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name, twice(Rank) AS D FROM crew WHERE Rank > 3", "Ed Mercer/10; Kelly Grayson/8")]
        [DataRow("SELECT Name, twice(Rank) FROM crew WHERE Rank > 3", "Ed Mercer/10; Kelly Grayson/8")]
        [DataRow("SELECT c.Name, twice(c.Rank) AS D FROM crew c WHERE c.Rank > 3", "Ed Mercer/10; Kelly Grayson/8")]
        [DataRow("SELECT Name, twice(Rank) AS D FROM crew WHERE Rank > 2 ORDER BY D DESC, Name", "Ed Mercer/10; Kelly Grayson/8; Bortus/6; Talla Keyali/6")]
        [DataRow("SELECT Name, twice(Rank) FROM crew WHERE Rank > 2 ORDER BY twice, Name", "Bortus/6; Talla Keyali/6; Kelly Grayson/8; Ed Mercer/10")]
        [DataRow("SELECT Species, twice(Rank) AS D FROM crew GROUP BY Species, Rank ORDER BY D, Species", "Human/4; Kaylon/4; Moclan/6; Xelayan/6; Human/8; Human/10")]
        [DataRow("SELECT Species, twice(Rank) AS D FROM crew GROUP BY Species, Rank HAVING D > 6 ORDER BY D", "Human/8; Human/10")]
        [DataRow("SELECT s.Name, twice(c.Rank) AS D FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 3", "Orville/6; Roosevelt/6")]
        public void FunctionColumnsAreCalculatedPerRow(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, int>>(sql, Twice);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, int>>(sql, Twice);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        public void FunctionColumnsAreNamedByAliasOrFunction()
        {
            // Act
            var row = Orville.Tables().Query("SELECT Name, twice(Rank) AS D, twice(Age) FROM crew WHERE Rank = 5", Twice).Single();

            // Assert
            Assert.AreEqual("Ed Mercer", row["Name"]);
            Assert.AreEqual(10, row["D"]);
            Assert.AreEqual(82, row["twice"]);
        }

        [TestMethod]
        public void FunctionColumnArgumentsMustBeGrouped()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query("SELECT Species, twice(Rank) FROM crew GROUP BY Species", Twice).ToList());
            Assert.Contains("The query contains the field 'Rank' that is not matched between the select list and group by clause.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Species, twice(twice(Rank)) FROM crew GROUP BY Species", "Rank")]
        [DataRow("SELECT Species, twice(Rank) + 1 AS X FROM crew GROUP BY Species", "Rank")]
        [DataRow("SELECT Species, twice(Rank + Age) AS X FROM crew GROUP BY Species, Rank", "Age")]
        public void NestedFunctionArgumentsMustBeGrouped(string sql, string field)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql, Twice).ToList());
            Assert.Contains("The query contains the field '" + field + "' that is not matched between the select list and group by clause.", e.Message);
        }

        [TestMethod]
        public void NestedFunctionColumnsReadGroupedFields()
        {
            // Act
            var result = Orville.Tables().Query<System.Tuple<string, int>>("SELECT Species, twice(twice(Rank)) AS Q FROM crew GROUP BY Species, Rank ORDER BY Q, Species", Twice);

            // Assert
            Assert.AreEqual("Human/8; Kaylon/8; Moclan/12; Xelayan/12; Human/16; Human/20", Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name FROM crew c JOIN ships s ON c.ShipId = s.Id", "Name")]
        [DataRow("SELECT Name, Name FROM crew", "Name")]
        [DataRow("SELECT Name, Species AS Name FROM crew", "Name")]
        [DataRow("SELECT Rank * 2 AS D, Age AS D FROM crew", "D")]
        [DataRow("SELECT twice(Rank), twice(Age) FROM crew", "twice")]
        [DataRow("SELECT Species, MAX(Rank) AS N, MIN(Rank) AS N FROM crew GROUP BY Species", "N")]
        [DataRow("SELECT COUNT(*) AS N, MAX(Rank) AS N FROM crew", "N")]
        public void NamedResultsNeedDistinctColumnNames(string sql, string name)
        {
            // Arrange
            var message = "The select list has more than one column named '" + name + "'; give each a different name with AS.";

            // Act & Assert
            var dictionaries = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql, Twice).ToList());
            var dynamic = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query<object>(sql, Twice).ToList());
            Assert.AreEqual(message, dictionaries.Message);
            Assert.AreEqual(message, dynamic.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name, Name FROM crew WHERE Rank = 5", "Ed Mercer/Ed Mercer")]
        [DataRow("SELECT Name, Species AS Name FROM crew WHERE Rank = 5", "Ed Mercer/Human")]
        [DataRow("SELECT Name, Species AS Name FROM crew WHERE Rank > 3 ORDER BY 2, 1", "Ed Mercer/Human; Kelly Grayson/Human")]
        public void TuplesTakeColumnsWithTheSameName(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) AS N, MAX(Rank) AS N FROM crew", "6/5")]
        [DataRow("SELECT MIN(Rank) AS N, MAX(Rank) AS N FROM crew WHERE Species = 'Human'", "2/5")]
        public void AggregateTuplesTakeColumnsWithTheSameName(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<System.Tuple<int, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void GroupedTuplesTakeColumnsWithTheSameName()
        {
            // Act
            var result = Orville.Tables().Query<System.Tuple<string, int, int>>("SELECT Species, MAX(Rank) AS N, MIN(Rank) AS N FROM crew GROUP BY Species ORDER BY Species");

            // Assert
            Assert.AreEqual("Human/5/2; Kaylon/2/2; Moclan/3/3; Xelayan/3/3", Orville.Rows(result));
        }

        [TestMethod]
        public void ColumnsThatNoConstructorTakesThrow()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query<string>("SELECT Name, Species FROM crew").ToList());
            Assert.AreEqual("String has no constructor that takes the 2 columns of the select list.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT [c].[Species], COUNT(*) FROM crew c GROUP BY [c].[Species] HAVING [c].[Species] <> 'Human' ORDER BY [c].[Species]", "Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT [c].[Name], c.[Rank] FROM crew [c] WHERE [c].[Rank] > 3 ORDER BY c.[Rank]", "Kelly Grayson/4; Ed Mercer/5")]
        [DataRow("SELECT [s].[Name], COUNT(*) FROM crew c JOIN ships s ON [c].[ShipId] = [s].[Id] GROUP BY [s].[Name] ORDER BY [s].[Name]", "Orville/4; Roosevelt/1")]
        public void BracketedQualifiedNamesResolveInEveryClause(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, int>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        public void BracketedQualifiedNamesResolveInTypedJoins()
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Ship, System.Tuple<string, string>>("SELECT [this].[Name], [that].[Name] AS Ship FROM this INNER JOIN that ON [this].[ShipId] = [that].[Id] WHERE [that].[Id] = 2", Orville.Ships());

            // Assert
            Assert.AreEqual("Talla Keyali/Roosevelt", Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT s.Class FROM classes k WHERE k.Role = 'Research') = 'Science'", "Pembroke/Science")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT c.ShipId FROM ships s WHERE s.Id = c.ShipId AND s.Class = 'Exploratory') = 2", "Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE 'Moclan' IN (SELECT c.Species FROM ships s WHERE s.Id = c.ShipId)", "Bortus/Moclan")]
        public void OuterReferencesBindInASubquerySelectListOverEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c WHERE crew.Rank > 3", "crew in crew.Rank isn't a source or alias of this query")]
        [DataRow("SELECT crew.Name FROM crew c", "crew in crew.Name isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c ORDER BY crew.Name", "crew in crew.Name isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE ships.Id = 2", "ships in ships.Id isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = ships.Id", "ships in ships.Id isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = ships.Id", "JOIN ON c.ShipId = ships.Id must compare a field of the joined source with a field of an earlier source.")]
        public void AnAliasHidesItsTableName(string sql, string message)
        {
            // Act
            var typed = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            var dictionaries = Assert.ThrowsExactly<SqlException>(() => Orville.DictionaryTables().Query(sql).ToList());

            // Assert
            Assert.Contains(message, typed.Message);
            Assert.Contains(message, dictionaries.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE crew.Rank > 3 ORDER BY crew.Name", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank > (SELECT MAX(crew.Rank) FROM crew WHERE crew.Species = c.Species AND crew.Name <> c.Name)", "Ed Mercer")]
        public void AnUnaliasedTableAnswersToItsName(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<string>(sql);
            var dictionaries = Orville.DictionaryTables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed), "typed tables");
            Assert.AreEqual(expected, string.Join(", ", dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Engine.RPM > 3000", "Orville, Pembroke")]
        [DataRow("SELECT * FROM this WHERE Engine.RPM > 3000 ORDER BY Engine.RPM", "Pembroke, Orville")]
        [DataRow("SELECT * FROM this WHERE Engine.Stage = 2 AND Engine.RPM BETWEEN 4000 AND 5000", "Orville")]
        [DataRow("SELECT * FROM this WHERE Engine.Stage IN (1, 3)", "Roosevelt")]
        public void DottedKeysReadDictionaryAndDynamicRows(string sql, string expected)
        {
            // Act
            var dictionaries = Engines().Query(sql).Select(r => r["Name"]);
            var dynamics = Engines().Select(Expando).Query(sql).Select(r => ((IDictionary<string, object>)r)["Name"]);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", dictionaries), "dictionary rows");
            Assert.AreEqual(expected, string.Join(", ", dynamics), "dynamic rows");
        }

        [TestMethod]
        public void DottedKeysFollowTheKeyComparisonOfTheirRows()
        {
            // Arrange
            var sql = "SELECT * FROM this WHERE engine.rpm < 3000";

            // Act
            var dynamics = Engines().Select(Expando).Query(sql).Select(r => ((IDictionary<string, object>)r)["Name"]);
            var dictionaries = Assert.ThrowsExactly<SqlException>(() => Engines().Query(sql).ToList());

            // Assert
            Assert.AreEqual("Roosevelt", string.Join(", ", dynamics));
            Assert.AreEqual("engine in engine.rpm isn't a source or alias of this query, or a field of the row.", dictionaries.Message);
        }

        [TestMethod]
        public void DottedKeysFillColumnsAndGroups()
        {
            // Act
            var columns = Engines().Query<IDictionary<string, object>, System.Tuple<string, int>>("SELECT Name, Engine.RPM FROM this WHERE Engine.Stage = 2");
            var groups = Engines().Query<IDictionary<string, object>, System.Tuple<int, int>>("SELECT Engine.Stage, MAX(Engine.RPM) FROM this GROUP BY Engine.Stage ORDER BY Engine.Stage");

            // Assert
            Assert.AreEqual("Orville/4200; Pembroke/3600", Orville.Rows(columns));
            Assert.AreEqual("1/2800; 2/4200", Orville.Rows(groups));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM engines WHERE Engine.RPM > 3000", "Orville, Pembroke")]
        [DataRow("SELECT e.Name FROM engines e WHERE Engine.RPM > 3000 AND e.Name <> 'Pembroke'", "Orville")]
        [DataRow("SELECT e.Name FROM engines e WHERE e.Name IN (SELECT s.Name FROM ships s WHERE s.Class = 'Exploratory') AND Engine.Stage = 1", "Roosevelt")]
        [DataRow("SELECT s.Class FROM engines e JOIN ships s ON e.Name = s.Name WHERE Engine.RPM < 4000 ORDER BY s.Class", "Exploratory, Science")]
        [DataRow("SELECT e.Name FROM named e", "Real")]
        [DataRow("SELECT Name FROM named WHERE e.Name = 'Dotted'", "Real")]
        public void DottedKeysReadTablesAndOwnedAliasesComeFirst(string sql, string expected)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable>
            {
                ["engines"] = Engines(),
                ["ships"] = Orville.ShipDictionaries(),
                ["named"] = new List<IDictionary<string, object>> { new Dictionary<string, object> { ["Name"] = "Real", ["e.Name"] = "Dotted" } },
            };

            // Act
            var result = tables.Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        public void SqlExpressionsReadDottedKeys()
        {
            // Arrange
            var rows = Engines();

            // Act
            var fast = SqlExpression.Compile<IDictionary<string, object>>("Engine.RPM > 3000");
            var doubled = SqlExpression.Compile<IDictionary<string, object>>("Engine.RPM * 2");

            // Assert
            CollectionAssert.AreEqual(new object[] { true, false, true }, rows.Select(fast).ToArray());
            Assert.AreEqual(8400, doubled(rows[0]));
        }

        [TestMethod]
        public void MissingDottedKeysThrowSqlExceptionWhenARowIsRead()
        {
            // Arrange
            var message = "Engine in Engine.Torque isn't a source or alias of this query, or a field of the row.";
            var expression = SqlExpression.Compile<IDictionary<string, object>>("Engine.Torque > 1");

            // Act
            var where = Assert.ThrowsExactly<SqlException>(() => Engines().Query("SELECT * FROM this WHERE Engine.Torque > 1").ToList());
            var column = Assert.ThrowsExactly<SqlException>(() => Engines().Query("SELECT Name, Engine.Torque FROM this").ToList());
            var compiled = Assert.ThrowsExactly<SqlException>(() => expression(Engines()[0]));

            // Assert
            Assert.AreEqual(message, where.Message);
            Assert.AreEqual(message, column.Message);
            Assert.AreEqual(message, compiled.Message);
        }

        [TestMethod]
        public void TypedRowsRejectUnknownQualifiersWhenCompiled()
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query("SELECT * FROM this WHERE Engine.RPM > 3000"));

            // Assert
            Assert.AreEqual("Engine in Engine.RPM isn't a source or alias of this query.", e.Message);
        }

        [TestMethod]
        public void CompiledScalarJoinQueriesRunTheJoin()
        {
            // Arrange
            var query = new ScalarJoinQuery<CrewMember, Ship, int>("SELECT COUNT(*) FROM this c LEFT JOIN that s ON c.ShipId = s.Id WHERE s.Id IS NULL OR s.Class = 'Exploratory'");
            query.Compile();

            // Act
            var count = query.Evaluate(Orville.Crew(), Orville.Ships());
            var name = Orville.Crew().QueryScalar<CrewMember, Ship, string>("SELECT that.Name FROM this JOIN that ON this.ShipId = that.Id WHERE this.Rank = 5", Orville.Ships());

            // Assert
            Assert.AreEqual(6, count);
            Assert.AreEqual("Orville", name);
        }

        [TestMethod]
        public void ExpandoRowsReadFieldsInEveryClause()
        {
            // Arrange
            var expandos = Orville.CrewExpandos();
            var tables = new Dictionary<string, IEnumerable>
            {
                ["crew"] = expandos,
                ["engines"] = Engines().Select(Expando).Cast<System.Dynamic.ExpandoObject>().ToList(),
            };

            // Act
            var typed = expandos.Query<System.Dynamic.ExpandoObject, System.Tuple<string, string>>("SELECT name, Species FROM this WHERE RANK = 5");
            var groups = tables.Query<System.Tuple<string, int>>("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) > 1");
            var subquery = tables.Query<string>("SELECT c.Name FROM crew c WHERE c.Rank = (SELECT MAX(Rank) FROM crew)");
            var dotted = tables.Query<string>("SELECT Name FROM engines WHERE Engine.RPM > 3000 ORDER BY Engine.RPM");

            // Assert
            Assert.AreEqual("Ed Mercer/Human", Orville.Rows(typed));
            Assert.AreEqual("Human/3", Orville.Rows(groups));
            Assert.AreEqual("Ed Mercer", string.Join(", ", subquery));
            Assert.AreEqual("Pembroke, Orville", string.Join(", ", dotted));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank > 3", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM this WHERE Rank > 2 AND Species <> 'Human'", "Bortus, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Rank > 4 OR Species = 'Moclan'", "Ed Mercer, Bortus")]
        [DataRow("SELECT * FROM this WHERE Rank IN (3, 5)", "Ed Mercer, Bortus, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Rank BETWEEN 3 AND 4", "Kelly Grayson, Bortus, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Name > 'K'", "Kelly Grayson, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE NOT Rank > 2", "<null>, Gordon Malloy, Isaac")]
        [DataRow("SELECT * FROM this", "Ed Mercer, Kelly Grayson, <null>, Bortus, Gordon Malloy, Talla Keyali, Isaac")]
        public void NullRowsOfATypedListMatchNoComparison(string sql, string expected)
        {
            // Arrange
            var crew = CrewWithANullRow();

            // Act
            var result = crew.Query(sql).Select(c => c == null ? "<null>" : c.Name);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        public void NullRowsOfATypedListAreFilteredBeforeColumnsAndGroups()
        {
            // Arrange
            var crew = CrewWithANullRow();

            // Act
            var names = crew.Query<CrewMember, string>("SELECT Name FROM this WHERE Rank > 3");
            var groups = crew.Query<CrewMember, System.Tuple<string, int>>("SELECT Species, COUNT(*) FROM this WHERE Rank > 0 GROUP BY Species");
            var count = crew.QueryScalar<CrewMember, int>("SELECT COUNT(*) FROM this");
            var top = crew.QueryScalar<CrewMember, int>("SELECT MAX(Rank) FROM this WHERE Rank > 0");

            // Assert
            Assert.AreEqual("Ed Mercer, Kelly Grayson", string.Join(", ", names));
            Assert.AreEqual("Human/3; Moclan/1; Xelayan/1; Kaylon/1", Orville.Rows(groups));
            Assert.AreEqual(7, count);
            Assert.AreEqual(5, top);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this")]
        [DataRow("SELECT Species, COUNT(*) FROM this GROUP BY Species")]
        [DataRow("SELECT * FROM this ORDER BY Name")]
        public void NullRowsOfATypedListFailReadsNoComparisonFiltered(string sql)
        {
            // Arrange
            var crew = CrewWithANullRow();

            // Act & Assert
            Assert.ThrowsExactly<System.NullReferenceException>(() => crew.Query<CrewMember, object>(sql).ToList());
        }

        [TestMethod]
        [DataRow("SELECT Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id")]
        [DataRow("SELECT Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id WHERE this.Rank > 2")]
        public void NullRowsOfATypedListFailJoins(string sql)
        {
            // Arrange
            var crew = CrewWithANullRow();

            // Act & Assert
            Assert.ThrowsExactly<System.NullReferenceException>(() => crew.Query<CrewMember, Ship, System.Tuple<string, string>>(sql, Orville.Ships()).ToList());
        }

        private static List<CrewMember> CrewWithANullRow()
        {
            var crew = Orville.Crew();
            crew.Insert(2, null);

            return crew;
        }

        private static List<IDictionary<string, object>> Engines()
        {
            return
            [
                new Dictionary<string, object> { ["Name"] = "Orville", ["Engine.RPM"] = 4200, ["Engine.Stage"] = 2 },
                new Dictionary<string, object> { ["Name"] = "Roosevelt", ["Engine.RPM"] = 2800, ["Engine.Stage"] = 1 },
                new Dictionary<string, object> { ["Name"] = "Pembroke", ["Engine.RPM"] = 3600, ["Engine.Stage"] = 2 },
            ];
        }

        private static object Expando(IDictionary<string, object> values)
        {
            var expando = new System.Dynamic.ExpandoObject();
            var fields = (IDictionary<string, object>)expando;
            foreach (var value in values)
            {
                fields.Add(value);
            }

            return expando;
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId IN (SELECT Id FROM ships WHERE Class = 'Exploratory')", "Ed Mercer/Human; Kelly Grayson/Human; Bortus/Moclan; Gordon Malloy/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id IN (SELECT ShipId FROM crew WHERE Rank > 2)", "Orville/Exploratory; Roosevelt/Exploratory")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id NOT IN (SELECT ShipId FROM crew WHERE ShipId IS NOT NULL)", "Pembroke/Science")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id NOT IN (SELECT ShipId FROM crew)", "")]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships WHERE Id = 2)", "Ed Mercer/Human; Kelly Grayson/Human; Bortus/Moclan; Gordon Malloy/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships WHERE Id > 5) AND Rank = 2", "Gordon Malloy/Human; Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM crew WHERE Species IN (SELECT Species FROM planets WHERE Gravity > 1.2)", "Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay IN (SELECT Gravity FROM planets)", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank IN (SELECT COUNT(*) FROM ships)", "Bortus/Moclan; Talla Keyali/Xelayan")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE 5 IN (SELECT c.Rank FROM crew c WHERE c.ShipId = s.Id)", "Orville/Exploratory")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE 2 NOT IN (SELECT c.Rank FROM crew c WHERE c.ShipId = s.Id)", "Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Species IN (SELECT p.Species FROM planets p WHERE p.Moons + 1 = c.Rank)", "Gordon Malloy/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Class IN (SELECT Class FROM classes WHERE Role = 'Exploration') AND c.Rank >= 4", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        public void InSubqueryMatchesItsRows(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) = 1 AND Species IN (SELECT Species FROM planets WHERE Moons > 1) ORDER BY Species", "Kaylon, 1; Xelayan, 1")]
        [DataRow("SELECT s.Name, s.Id IN (SELECT ShipId FROM crew) AS Crewed FROM ships s", "Orville, True; Roosevelt, True; Pembroke, False")]
        [DataRow("SELECT COUNT(*) AS N FROM crew WHERE ShipId IN (SELECT Id FROM ships WHERE Class = 'Exploratory')", "5")]
        public void InSubqueryWorksInEveryClause(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query(sql);

            // Assert
            Assert.AreEqual(expected, Values(result));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank IN (SELECT Rank FROM this WHERE Species = 'Moclan')", "Bortus, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Rank NOT IN (SELECT c.Rank FROM crew c WHERE c.Species = 'Human')", "Bortus, Talla Keyali")]
        public void InSubqueryOverTheQuerySource(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query(sql).Select(c => c.Name);
            var dictionaries = Orville.CrewDictionaries().Query(sql).Select(c => c["Name"]);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE ShipId IN (SELECT Id FROM ships WHERE seen(Id))", 3, "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank NOT IN (SELECT Rank FROM crew WHERE seen(Rank) AND Species = 'Human')", 6, "Bortus, Talla Keyali")]
        [DataRow("SELECT s.Name FROM ships s WHERE 5 IN (SELECT c.Rank FROM crew c WHERE seen(c.Rank) AND c.ShipId = s.Id)", 18, "Orville")]
        public void InSubqueryRunsOncePerPassUnlessCorrelated(string sql, int reads, string expected)
        {
            // Arrange
            var seen = new List<object>();
            var record = (System.Func<object, bool>)(value =>
            {
                seen.Add(value);
                return true;
            });
            var functions = new ISqlFunction[]
            {
                new TestFunction("seen", arguments => Expression.Invoke(Expression.Constant(record), Expression.Convert(arguments[0].Expression, typeof(object)))),
            };
            var result = Orville.Tables().Query<string>(sql, functions);

            // Act
            var first = string.Join(", ", result);
            var firstReads = seen.Count;
            var second = string.Join(", ", result);

            // Assert
            Assert.AreEqual(expected, first);
            Assert.AreEqual(expected, second);
            Assert.AreEqual(reads, firstReads);
            Assert.HasCount(reads * 2, seen);
        }

        [TestMethod]
        public void InSubquerySeesChangesWhenTheResultIsEnumeratedAgain()
        {
            // Arrange
            var tables = Orville.Tables();
            var ships = (List<Ship>)tables["ships"];
            var result = tables.Query<string>("SELECT Name FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships WHERE Id < 3)");

            // Act
            var before = string.Join(", ", result);
            ships.RemoveAt(1);
            var after = string.Join(", ", result);

            // Assert
            Assert.AreEqual("", before);
            Assert.AreEqual("Talla Keyali", after);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE ShipId IN (SELECT Id, Name FROM ships)", "The subquery of IN must select exactly one column.")]
        [DataRow("SELECT Name FROM crew WHERE ShipId IN (SELECT * FROM ships)", "The subquery of IN must select exactly one column.")]
        [DataRow("SELECT Name FROM crew WHERE ShipId NOT IN (SELECT * FROM ships)", "The subquery of IN must select exactly one column.")]
        [DataRow("SELECT Name FROM crew WHERE ShipId IN (SELECT Id FROM nowhere)", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT Name FROM crew WHERE ShipId IN (SELECT Id FROM ships WHERE Id = (SELECT Id FROM ships))", "The subquery returned more than one row.")]
        public void InSubqueryMistakesThrow(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id)", "Orville/Exploratory; Roosevelt/Exploratory")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE NOT EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id)", "Pembroke/Science")]
        [DataRow("SELECT Name, Species FROM crew WHERE EXISTS (SELECT * FROM ships WHERE Class = 'Science') AND Rank = 5", "Ed Mercer/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE EXISTS (SELECT * FROM ships WHERE Class = 'Warship')", "")]
        [DataRow("SELECT Name, Species FROM crew WHERE NOT EXISTS (SELECT * FROM ships WHERE Class = 'Warship') AND Rank = 4", "Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE EXISTS (SELECT COUNT(*) FROM ships WHERE Class = 'Warship') AND Rank = 5", "Ed Mercer/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE EXISTS (SELECT p.Name FROM planets p WHERE p.Species = c.Species AND p.Moons >= 2)", "Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE EXISTS (SELECT c.Species, COUNT(*) FROM crew c WHERE c.ShipId = s.Id GROUP BY c.Species HAVING COUNT(*) > 1)", "Orville/Exploratory")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id AND EXISTS (SELECT * FROM planets p WHERE p.Species = c.Species AND p.Moons = 0))", "Orville/Exploratory")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE EXISTS (SELECT * FROM classes k WHERE k.Class = s.Class AND k.Role = 'Exploration') AND c.Rank > 3", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        public void ExistsTestsWhetherTheSubqueryHasRows(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) > 0 AND EXISTS (SELECT * FROM planets p WHERE p.Species = c.Species AND p.Moons > 1) ORDER BY c.Species", "Kaylon, 1; Xelayan, 1")]
        [DataRow("SELECT s.Name, EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id) AS Crewed FROM ships s", "Orville, True; Roosevelt, True; Pembroke, False")]
        [DataRow("SELECT COUNT(*) AS N FROM ships s WHERE NOT EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id)", "1")]
        public void ExistsWorksInEveryClause(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query(sql);

            // Assert
            Assert.AreEqual(expected, Values(result));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this c WHERE EXISTS (SELECT * FROM this k WHERE k.Species = c.Species AND k.Name <> c.Name)", "Ed Mercer, Kelly Grayson, Gordon Malloy")]
        [DataRow("SELECT * FROM this c WHERE NOT EXISTS (SELECT * FROM this k WHERE k.Species = c.Species AND k.Name <> c.Name)", "Bortus, Talla Keyali, Isaac")]
        public void ExistsOverTheQuerySource(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query(sql).Select(c => c.Name);
            var dictionaries = Orville.CrewDictionaries().Query(sql).Select(c => c["Name"]);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE EXISTS (SELECT * FROM ships WHERE seen(Id))", 1, "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE EXISTS (SELECT * FROM ships WHERE seen(Id) AND Class = 'Science')", 3, "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT s.Name FROM ships s WHERE EXISTS (SELECT * FROM crew c WHERE seen(c.Name) AND c.ShipId = s.Id)", 12, "Orville, Roosevelt")]
        public void ExistsStopsAtTheFirstRowAndRunsOncePerPassUnlessCorrelated(string sql, int reads, string expected)
        {
            // Arrange
            var seen = new List<object>();
            var record = (System.Func<object, bool>)(value =>
            {
                seen.Add(value);
                return true;
            });
            var functions = new ISqlFunction[]
            {
                new TestFunction("seen", arguments => Expression.Invoke(Expression.Constant(record), Expression.Convert(arguments[0].Expression, typeof(object)))),
            };
            var result = Orville.Tables().Query<string>(sql, functions);

            // Act
            var first = string.Join(", ", result);
            var firstReads = seen.Count;
            var second = string.Join(", ", result);

            // Assert
            Assert.AreEqual(expected, first);
            Assert.AreEqual(expected, second);
            Assert.AreEqual(reads, firstReads);
            Assert.HasCount(reads * 2, seen);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE EXISTS (SELECT * FROM nowhere)", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT Name FROM crew WHERE EXISTS (SELECT Id FROM ships WHERE Id = (SELECT Id FROM ships))", "The subquery returned more than one row.")]
        [DataRow("SELECT Name FROM crew WHERE NOT EXISTS (SELECT Species, COUNT(*) FROM crew)", "Your query contains fields and aggregates but no GROUP BY clause.")]
        [DataRow("SELECT Name FROM crew WHERE EXISTS SELECT * FROM ships", "SQL parse error")]
        public void ExistsMistakesThrow(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay > ALL (SELECT Pay FROM crew WHERE Rank < 3)", "Ed Mercer/Human; Kelly Grayson/Human; Bortus/Moclan; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay = ANY (SELECT Gravity FROM planets)", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = SOME (SELECT Moons FROM planets WHERE Moons > 1)", "Bortus/Moclan; Gordon Malloy/Human; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank >= ALL (SELECT Rank FROM crew)", "Ed Mercer/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank < ANY (SELECT Rank FROM crew WHERE Species = 'Moclan')", "Gordon Malloy/Human; Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank <= ALL (SELECT Rank FROM crew WHERE Species = 'Human')", "Gordon Malloy/Human; Isaac/Kaylon")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id <> ALL (SELECT ShipId FROM crew)", "")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id != ALL (SELECT ShipId FROM crew WHERE ShipId IS NOT NULL)", "Pembroke/Science")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id <> ANY (SELECT ShipId FROM crew)", "Orville/Exploratory; Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > ALL (SELECT Rank FROM crew WHERE Species = 'Vulcan') AND Rank = 4", "Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > ANY (SELECT Rank FROM crew WHERE Species = 'Vulcan')", "")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id <= ALL (SELECT ShipId FROM crew WHERE Rank > 2)", "Orville/Exploratory")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id <= ALL (SELECT ShipId FROM crew WHERE Rank > 2 OR ShipId IS NULL)", "")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id >= ANY (SELECT ShipId FROM crew WHERE Rank = 2)", "Orville/Exploratory; Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId < ANY (SELECT Id FROM ships)", "Ed Mercer/Human; Kelly Grayson/Human; Bortus/Moclan; Gordon Malloy/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Species > ALL (SELECT Species FROM planets WHERE Moons < 2)", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Name > ANY (SELECT Rank FROM crew)", "")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank >= ALL (SELECT k.Rank FROM crew k WHERE k.Species = c.Species)", "Ed Mercer/Human; Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE 4 < ANY (SELECT c.Rank FROM crew c WHERE c.ShipId = s.Id)", "Orville/Exploratory")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Pay > ALL (SELECT p.Gravity FROM planets p)", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        public void QuantifiedComparisonsTestEverySubqueryValue(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM ships WHERE Id = ANY (SELECT ShipId FROM crew)", "SELECT Name FROM ships WHERE Id IN (SELECT ShipId FROM crew)")]
        [DataRow("SELECT Name FROM ships WHERE Id <> ALL (SELECT ShipId FROM crew)", "SELECT Name FROM ships WHERE Id NOT IN (SELECT ShipId FROM crew)")]
        [DataRow("SELECT Name FROM crew WHERE ShipId <> ALL (SELECT Id FROM ships WHERE Id = 2)", "SELECT Name FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships WHERE Id = 2)")]
        public void QuantifiedEqualityMatchesIn(string quantified, string membership)
        {
            // Act
            var viaQuantifier = Orville.Tables().Query<string>(quantified);
            var viaIn = Orville.Tables().Query<string>(membership);

            // Assert
            Assert.AreEqual(string.Join(", ", viaIn), string.Join(", ", viaQuantifier));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > ALL (SELECT Moons FROM planets WHERE Moons < 3) ORDER BY Species", "Human, 3")]
        [DataRow("SELECT s.Name, s.Id < ALL (SELECT ShipId FROM crew WHERE ShipId > 1) AS Lowest FROM ships s", "Orville, True; Roosevelt, False; Pembroke, False")]
        [DataRow("SELECT COUNT(*) AS N FROM crew WHERE Rank > SOME (SELECT Rank FROM crew WHERE Species = 'Moclan')", "2")]
        public void QuantifiedComparisonsWorkInEveryClause(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query(sql);

            // Assert
            Assert.AreEqual(expected, Values(result));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank >= ALL (SELECT Rank FROM this)", "Ed Mercer")]
        [DataRow("SELECT * FROM this c WHERE c.Rank < ANY (SELECT k.Rank FROM this k WHERE k.Species = c.Species)", "Kelly Grayson, Gordon Malloy")]
        public void QuantifiedComparisonsOverTheQuerySource(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query(sql).Select(c => c.Name);
            var dictionaries = Orville.CrewDictionaries().Query(sql).Select(c => c["Name"]);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank >= ALL (SELECT Rank FROM crew WHERE seen(Rank))", 6, "Ed Mercer")]
        [DataRow("SELECT s.Name FROM ships s WHERE 4 < ANY (SELECT c.Rank FROM crew c WHERE seen(c.Rank) AND c.ShipId = s.Id)", 18, "Orville")]
        public void QuantifiedComparisonsRunOncePerPassUnlessCorrelated(string sql, int reads, string expected)
        {
            // Arrange
            var seen = new List<object>();
            var record = (System.Func<object, bool>)(value =>
            {
                seen.Add(value);
                return true;
            });
            var functions = new ISqlFunction[]
            {
                new TestFunction("seen", arguments => Expression.Invoke(Expression.Constant(record), Expression.Convert(arguments[0].Expression, typeof(object)))),
            };

            // Act
            var result = string.Join(", ", Orville.Tables().Query<string>(sql, functions));

            // Assert
            Assert.AreEqual(expected, result);
            Assert.HasCount(reads, seen);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank > ALL (SELECT Rank, Age FROM crew)", "The subquery of ALL must select exactly one column.")]
        [DataRow("SELECT Name FROM crew WHERE Rank = ANY (SELECT * FROM crew)", "The subquery of ANY must select exactly one column.")]
        [DataRow("SELECT Name FROM crew WHERE Rank < some (SELECT * FROM crew)", "The subquery of SOME must select exactly one column.")]
        [DataRow("SELECT Name FROM crew WHERE Rank > ALL (SELECT Rank FROM nowhere)", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT Name FROM crew WHERE Rank > ALL (SELECT Id FROM ships WHERE Id = (SELECT Id FROM ships))", "The subquery returned more than one row.")]
        [DataRow("SELECT Name FROM crew WHERE Rank > ALL (1, 2)", "SQL parse error")]
        public void QuantifiedComparisonMistakesThrow(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank > 3 UNION SELECT Name FROM ships", "Ed Mercer, Kelly Grayson, Orville, Roosevelt, Pembroke")]
        [DataRow("SELECT Species FROM crew UNION SELECT Species FROM planets", "Human, Moclan, Xelayan, Kaylon")]
        [DataRow("SELECT Species FROM crew UNION ALL SELECT Species FROM planets WHERE Moons > 1", "Human, Human, Moclan, Human, Xelayan, Kaylon, Xelayan, Kaylon")]
        [DataRow("SELECT Species FROM crew WHERE Rank > 3 UNION ALL SELECT Species FROM crew WHERE Rank = 5 UNION SELECT Species FROM planets WHERE Moons = 0", "Human, Moclan")]
        [DataRow("SELECT Species FROM crew WHERE Rank > 3 UNION SELECT Species FROM planets WHERE Moons = 0 UNION ALL SELECT Species FROM crew WHERE Rank = 5", "Human, Moclan, Human")]
        [DataRow("SELECT Name FROM ships UNION SELECT Name FROM planets ORDER BY Name", "Earth, Kaylon 1, Moclus, Orville, Pembroke, Roosevelt, Xelaya")]
        [DataRow("SELECT Name FROM ships UNION SELECT Name FROM planets WHERE Moons > 1 ORDER BY 1 DESC", "Xelaya, Roosevelt, Pembroke, Orville, Kaylon 1")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Name = 'Roosevelt' UNION SELECT Name FROM crew WHERE ShipId IS NULL", "Talla Keyali, Isaac")]
        [DataRow("SELECT c.Name FROM crew c, ships s WHERE c.ShipId = s.Id AND s.Id = 2 UNION SELECT s.Name FROM ships s WHERE s.Id = 3", "Talla Keyali, Pembroke")]
        [DataRow("SELECT Name FROM crew WHERE Rank = (SELECT COUNT(*) FROM ships) + 2 UNION SELECT Name FROM ships WHERE Id IN (SELECT ShipId FROM crew WHERE Rank = 3)", "Ed Mercer, Orville, Roosevelt")]
        public void UnionCombinesItsParts(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<string>(sql);
            var dictionaries = Orville.DictionaryTables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > 3 UNION SELECT Name, Species FROM planets ORDER BY 2, 1", "Earth/Human; Ed Mercer/Human; Kelly Grayson/Human; Kaylon 1/Kaylon; Moclus/Moclan; Xelaya/Xelayan")]
        [DataRow("SELECT c.Species, s.Class FROM crew c JOIN ships s ON c.ShipId = s.Id UNION SELECT Species, 'Exploratory' AS Class FROM planets", "Human/Exploratory; Moclan/Exploratory; Xelayan/Exploratory; Kaylon/Exploratory")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = 5 UNION SELECT Class, Name FROM ships WHERE Id = 3", "Ed Mercer/Human; Science/Pembroke")]
        public void UnionIntoTuplesIsPositional(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<System.Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<System.Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name, Rank FROM crew WHERE Rank > 3 UNION SELECT Name, Moons AS Rank FROM planets WHERE Moons > 2 ORDER BY Rank DESC", "Ed Mercer, 5; Kelly Grayson, 4; Kaylon 1, 3")]
        [DataRow("SELECT Pay AS V FROM crew WHERE Pay = 3.5 UNION SELECT Gravity AS V FROM planets WHERE Gravity = 3.5", "3.5")]
        [DataRow("SELECT Species, COUNT(*) AS N FROM crew GROUP BY Species UNION SELECT Species, Moons AS N FROM planets ORDER BY 1, 2", "Human, 1; Human, 3; Kaylon, 1; Kaylon, 3; Moclan, 0; Moclan, 1; Xelayan, 1; Xelayan, 2")]
        public void UnionIntoDictionariesMatchesByValue(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query(sql);

            // Assert
            Assert.AreEqual(expected, Values(result));
        }

        [TestMethod]
        public void UnionFillsTypedDynamicAndScalarResults()
        {
            // Arrange
            const string Postings = "SELECT c.Name, s.Name AS Ship, c.Rank FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank > 3 "
                + "UNION SELECT c.Name, s.Name AS Ship, c.Rank FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Species = 'Human' ORDER BY Rank DESC";
            var tables = Orville.Tables();

            // Act
            var postings = tables.Query<Posting>(Postings).ToList();
            var expandos = tables.Query<dynamic>("SELECT Name, Rank FROM crew WHERE Rank > 3 UNION SELECT Name, Moons AS Rank FROM planets WHERE Moons > 2").ToList();
            var numbers = tables.Query<int>("SELECT Rank FROM crew UNION SELECT Moons FROM planets ORDER BY 1");

            // Assert
            Assert.AreEqual("Ed Mercer/Orville/5; Kelly Grayson/Orville/4; Gordon Malloy/Orville/2", string.Join("; ", postings.Select(p => p.Name + "/" + p.Ship + "/" + p.Rank)));
            Assert.HasCount(3, expandos);
            Assert.AreEqual("Kaylon 1", expandos[2].Name);
            Assert.AreEqual(3, expandos[2].Rank);
            Assert.AreEqual("0, 1, 2, 3, 4, 5", string.Join(", ", numbers));
        }

        [TestMethod]
        public void UnionOfWholeRowsComparesTheirColumns()
        {
            // Arrange
            var crew = Orville.Crew();
            var tables = new Dictionary<string, IEnumerable> { ["crew"] = crew, ["again"] = Orville.Crew() };

            // Act
            var viaQuery = crew.Query("SELECT * FROM this WHERE Rank > 3 UNION SELECT * FROM this WHERE Species = 'Human' ORDER BY 3").ToList();
            var twice = tables.Query<CrewMember>("SELECT * FROM crew UNION SELECT * FROM again").ToList();
            var twiceAll = tables.Query<CrewMember>("SELECT * FROM crew UNION ALL SELECT * FROM again").ToList();
            var dictionaries = Orville.CrewDictionaries().Query("SELECT * FROM this WHERE Rank > 3 UNION SELECT * FROM this WHERE Species = 'Human' ORDER BY Name");

            // Assert
            Assert.AreEqual("Gordon Malloy, Kelly Grayson, Ed Mercer", string.Join(", ", viaQuery.Select(c => c.Name)));
            Assert.AreSame(crew[0], viaQuery[2]);
            Assert.HasCount(6, twice);
            Assert.HasCount(12, twiceAll);
            Assert.AreEqual("Ed Mercer, Gordon Malloy, Kelly Grayson", string.Join(", ", dictionaries.Select(c => c["Name"])));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE Rank > 4 UNION SELECT Species FROM this WHERE Rank < 3", "Ed Mercer, Human, Kaylon")]
        [DataRow("SELECT Name FROM this WHERE Rank > 4 UNION ALL SELECT Name FROM this WHERE Species = 'Human' ORDER BY Name DESC", "Kelly Grayson, Gordon Malloy, Ed Mercer, Ed Mercer")]
        public void UnionThroughTheSingleTableQuery(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, string>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew UNION SELECT Name, Species FROM planets", "Each part of a UNION must select the same number of columns.")]
        [DataRow("SELECT * FROM crew UNION SELECT Name FROM ships", "Each part of a UNION must select the same number of columns.")]
        [DataRow("SELECT Name FROM crew ORDER BY Name UNION SELECT Name FROM ships", "Only the last part of a UNION can have an ORDER BY; it orders the whole UNION.")]
        [DataRow("SELECT Name FROM crew UNION SELECT Name FROM ships ORDER BY 2", "ORDER BY 2 is out of range; the select list has 1 columns.")]
        [DataRow("SELECT Name FROM crew UNION SELECT Name FROM ships ORDER BY Rank", "ORDER BY Rank isn't a column of the UNION.")]
        [DataRow("SELECT Name FROM crew UNION SELECT Class FROM ships", "Each part of a UNION must name its columns like the first part (Name); use AS to rename them.")]
        [DataRow("SELECT Name FROM crew UNION SELECT Name FROM nowhere", "'nowhere' isn't one of the sources")]
        public void UnionMistakesThrow(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        public void UnionOfWholeDictionaryRowsHasNoColumnPositions()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.DictionaryTables().Query("SELECT * FROM crew UNION SELECT * FROM ships ORDER BY 1").ToList());
            Assert.AreEqual("ORDER BY 1 needs a select list; SELECT * over dictionary or dynamic rows has no fixed column order.", e.Message);
        }

        [TestMethod]
        public void CompiledQueryRejectsUnion()
        {
            // Arrange
            var query = new Query<CrewMember, string>("SELECT Name FROM this UNION SELECT Species FROM this");

            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => query.Compile());
            Assert.AreEqual("A compiled query can't run a UNION; use the Query extension methods instead.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank > (SELECT AVG(Rank) FROM crew WHERE seen(Rank))", 6, "Ed Mercer; Kelly Grayson")]
        [DataRow("SELECT Name FROM crew WHERE Rank BETWEEN (SELECT MAX(Rank) FROM crew WHERE seen(Rank) AND Rank < 5) AND 5", 6, "Ed Mercer; Kelly Grayson")]
        [DataRow("SELECT Name FROM crew WHERE Rank IN (2, (SELECT MAX(Rank) FROM crew WHERE seen(Rank)))", 6, "Ed Mercer; Gordon Malloy; Isaac")]
        [DataRow("SELECT Name, (SELECT MAX(Rank) FROM crew WHERE seen(Rank)) AS Top FROM crew WHERE Rank > 3", 6, "Ed Mercer, 5; Kelly Grayson, 5")]
        [DataRow("SELECT Name, Rank - (SELECT MIN(Rank) FROM crew WHERE seen(Rank)) AS Above FROM crew WHERE Rank > 3", 6, "Ed Mercer, 3; Kelly Grayson, 2")]
        [DataRow("SELECT Name FROM crew WHERE Rank > (SELECT AVG(Rank) FROM crew WHERE seen(Rank) AND Age > (SELECT MIN(Age) FROM crew WHERE seen(Age)))", 12, "Ed Mercer; Kelly Grayson")]
        public void UncorrelatedScalarSubqueriesAreMaterializedOncePerPass(string sql, int reads, string expected)
        {
            // Act
            var (first, firstReads, second, allReads) = RunTwiceCountingReads(sql);

            // Assert
            Assert.AreEqual(expected, first);
            Assert.AreEqual(expected, second);
            Assert.AreEqual(reads, firstReads);
            Assert.AreEqual(reads * 2, allReads);
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank = (SELECT MAX(c2.Rank) FROM crew c2 WHERE seen(c2.Rank) AND c2.Species = c.Species)", 36, "Ed Mercer; Bortus; Talla Keyali; Isaac")]
        [DataRow("SELECT c.Name, (SELECT COUNT(*) FROM crew c2 WHERE seen(c2.Rank) AND c2.Species = c.Species) AS Kind FROM crew c WHERE c.Rank > 3", 12, "Ed Mercer, 3; Kelly Grayson, 3")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank > (SELECT AVG(c2.Rank) FROM crew c2 WHERE seen(c2.Rank) AND c2.Age > (SELECT MIN(c3.Age) FROM crew c3 WHERE c3.Species = c.Species))", 36, "Ed Mercer; Kelly Grayson; Bortus")]
        public void CorrelatedScalarSubqueriesRunOncePerOuterRow(string sql, int reads, string expected)
        {
            // Act
            var (first, firstReads, second, allReads) = RunTwiceCountingReads(sql);

            // Assert
            Assert.AreEqual(expected, first);
            Assert.AreEqual(expected, second);
            Assert.AreEqual(reads, firstReads);
            Assert.AreEqual(reads * 2, allReads);
        }

        [TestMethod]
        public void ScalarSubqueriesInHavingAreMaterializedOncePerPass()
        {
            // Act
            var (first, firstReads, second, allReads) = RunTwiceCountingReads("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > (SELECT MIN(Rank) FROM crew WHERE seen(Rank))");

            // Assert
            Assert.AreEqual("Human, 3", first);
            Assert.AreEqual("Human, 3", second);
            Assert.AreEqual(6, firstReads);
            Assert.AreEqual(12, allReads);
        }

        [TestMethod]
        public void MaterializedSubqueriesOfACompiledQueryReadTheEvaluatedSource()
        {
            // Arrange
            var seen = new List<object>();
            var record = (System.Func<object, bool>)(value =>
            {
                seen.Add(value);
                return true;
            });
            var functions = new ISqlFunction[]
            {
                new TestFunction("seen", arguments => Expression.Invoke(Expression.Constant(record), Expression.Convert(arguments[0].Expression, typeof(object)))),
            };
            var query = new Query<CrewMember, string>("SELECT Name FROM this WHERE Rank < (SELECT AVG(Rank) FROM this WHERE seen(Rank))", functions);
            query.Compile();
            var humans = Orville.Crew().Where(c => c.Species == "Human").ToList();

            // Act
            var fromHumans = string.Join(", ", query.Evaluate(humans));
            var humanReads = seen.Count;
            var fromEveryone = string.Join(", ", query.Evaluate(Orville.Crew()));

            // Assert
            Assert.AreEqual("Gordon Malloy", fromHumans);
            Assert.AreEqual("Bortus, Gordon Malloy, Talla Keyali, Isaac", fromEveryone);
            Assert.AreEqual(3, humanReads);
            Assert.HasCount(9, seen);
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Id = 3", "Yaphit/Pembroke", "")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id WHERE c.Name = 'Yaphit'", "Yaphit/Pembroke", "Yaphit/")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id WHERE s.Id = 3", "Yaphit/Pembroke", "")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id AND s.Id = 3", "Yaphit/Pembroke", "")]
        public void JoinQueriesReadTheirSourcesEachTimeTheResultIsEnumerated(string sql, string added, string removed)
        {
            // Arrange
            var tables = Orville.Tables();
            var crew = (List<CrewMember>)tables["crew"];
            var ships = (List<Ship>)tables["ships"];
            var result = tables.Query<System.Tuple<string, string>>(sql);
            crew.Add(new CrewMember { Name = "Yaphit", Species = "Gelatin", Rank = 2, Age = 50, Pay = 2m, ShipId = 3 });

            // Act
            var afterAdding = Orville.Rows(result);
            ships.RemoveAt(2);
            var afterRemoving = Orville.Rows(result);

            // Assert
            Assert.AreEqual(added, afterAdding);
            Assert.AreEqual(removed, afterRemoving);
        }

        [TestMethod]
        public void MaterializedScalarSubqueriesSeeChangesWhenTheResultIsEnumeratedAgain()
        {
            // Arrange
            var tables = Orville.Tables();
            var crew = (List<CrewMember>)tables["crew"];
            var result = tables.Query<string>("SELECT Name FROM crew WHERE Rank = (SELECT MAX(Rank) FROM crew)");

            // Act
            var before = string.Join(", ", result);
            crew.RemoveAt(0);
            var after = string.Join(", ", result);

            // Assert
            Assert.AreEqual("Ed Mercer", before);
            Assert.AreEqual("Kelly Grayson", after);
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank > (SELECT AVG(Rank) FROM crew)", "SELECT c.Name FROM crew c WHERE c.Rank > (SELECT AVG(Rank) FROM crew WHERE c.Name = c.Name)")]
        [DataRow("SELECT c.Name, (SELECT MAX(Rank) FROM crew WHERE Species = 'Human') AS Top FROM crew c", "SELECT c.Name, (SELECT MAX(Rank) FROM crew WHERE c.Name = c.Name AND Species = 'Human') AS Top FROM crew c")]
        [DataRow("SELECT c.Name, c.Rank - (SELECT MIN(Rank) FROM crew) AS Above FROM crew c", "SELECT c.Name, c.Rank - (SELECT MIN(Rank) FROM crew WHERE c.Name = c.Name) AS Above FROM crew c")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank BETWEEN (SELECT MIN(Rank) FROM crew) AND (SELECT AVG(Rank) FROM crew)", "SELECT c.Name FROM crew c WHERE c.Rank BETWEEN (SELECT MIN(Rank) FROM crew WHERE c.Name = c.Name) AND (SELECT AVG(Rank) FROM crew WHERE c.Name = c.Name)")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank IN (4, (SELECT MIN(Rank) FROM crew))", "SELECT c.Name FROM crew c WHERE c.Rank IN (4, (SELECT MIN(Rank) FROM crew WHERE c.Name = c.Name))")]
        [DataRow("SELECT c.Name, (SELECT MAX(Rank) FROM crew WHERE Rank > 9) AS Top FROM crew c", "SELECT c.Name, (SELECT MAX(Rank) FROM crew WHERE c.Name = c.Name AND Rank > 9) AS Top FROM crew c")]
        [DataRow("SELECT c.Name FROM crew c WHERE (SELECT MAX(Rank) FROM crew WHERE Rank > 9) IS NULL AND c.Rank = 5", "SELECT c.Name FROM crew c WHERE (SELECT MAX(Rank) FROM crew WHERE c.Name = c.Name AND Rank > 9) IS NULL AND c.Rank = 5")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) > (SELECT MIN(Rank) FROM crew)", "SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) > (SELECT MIN(Rank) FROM crew WHERE c.Species = c.Species)")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank > (SELECT AVG(Rank) FROM crew WHERE Age > (SELECT MIN(Age) FROM crew))", "SELECT c.Name FROM crew c WHERE c.Rank > (SELECT AVG(Rank) FROM crew WHERE Age > (SELECT MIN(Age) FROM crew WHERE c.Name = c.Name))")]
        [DataRow("SELECT COUNT(*) FROM crew c WHERE c.Rank < (SELECT MAX(Rank) FROM crew)", "SELECT COUNT(*) FROM crew c WHERE c.Rank < (SELECT MAX(Rank) FROM crew WHERE c.Name = c.Name)")]
        public void MaterializedScalarSubqueriesMatchPerRowEvaluation(string sql, string perRow)
        {
            // Act
            var typed = Values(Orville.Tables().Query(sql), "null");
            var typedPerRow = Values(Orville.Tables().Query(perRow), "null");
            var dictionaries = Values(Orville.DictionaryTables().Query(sql), "null");
            var dictionariesPerRow = Values(Orville.DictionaryTables().Query(perRow), "null");

            // Assert
            Assert.AreNotEqual("", typed);
            Assert.AreEqual(typedPerRow, typed, "typed tables");
            Assert.AreEqual(dictionariesPerRow, dictionaries, "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank = (SELECT Rank FROM crew WHERE Species = 'Human')")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank = (SELECT Rank FROM crew WHERE c.Name = c.Name AND Species = 'Human')")]
        [DataRow("SELECT c.Name, (SELECT Rank FROM crew WHERE Species = 'Human') AS R FROM crew c")]
        public void MaterializedScalarSubqueriesStillRejectMoreThanOneRow(string sql)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());

            // Assert
            Assert.AreEqual("The subquery returned more than one row.", e.Message);
        }

        private static (string First, int FirstReads, string Second, int AllReads) RunTwiceCountingReads(string sql)
        {
            var seen = new List<object>();
            var record = (System.Func<object, bool>)(value =>
            {
                seen.Add(value);
                return true;
            });
            var functions = new ISqlFunction[]
            {
                new TestFunction("seen", arguments => Expression.Invoke(Expression.Constant(record), Expression.Convert(arguments[0].Expression, typeof(object)))),
            };
            var result = Orville.Tables().Query(sql, functions);

            var first = Values(result);
            var firstReads = seen.Count;
            var second = Values(result);

            return (first, firstReads, second, seen.Count);
        }

        private static string Values(IEnumerable<IDictionary<string, object>> rows)
        {
            return Values(rows, "");
        }

        private static string Values(IEnumerable<IDictionary<string, object>> rows, string none)
        {
            return string.Join("; ", rows.Select(r => string.Join(", ", r.Values.Select(v => v == null ? none : System.Convert.ToString(v, CultureInfo.InvariantCulture)))));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Age > 1E2", "Isaac")]
        [DataRow("SELECT * FROM this WHERE Age > 1e2", "Isaac")]
        [DataRow("SELECT * FROM this WHERE Age = 4.5E1", "Bortus")]
        [DataRow("SELECT * FROM this WHERE Age < 3.1E+1", "Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Pay < 25E-1", "Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay * 1E1 = 55", "Ed Mercer")]
        public void ExponentLiteralsAreNumbers(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query(sql).Select(c => c.Name);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }
    }
}
