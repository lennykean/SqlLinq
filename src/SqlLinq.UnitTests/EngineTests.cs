using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class EngineTests
    {
        private const string AllPostings = "Ed Mercer/Orville; Kelly Grayson/Orville; Bortus/Orville; Gordon Malloy/Orville; Talla Keyali/Roosevelt";

        private static Dictionary<string, IEnumerable> Tables()
        {
            return new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.Crew(),
                ["ships"] = Orville.Ships(),
                ["classes"] = Orville.Classes(),
            };
        }

        private static Dictionary<string, IEnumerable> DictionaryTables()
        {
            return new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.CrewDictionaries(),
                ["ships"] = Orville.ShipDictionaries(),
                ["classes"] = Orville.Classes(),
            };
        }

        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank > 3", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT crew.Name, Species FROM crew WHERE crew.Rank > 3", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, c.Species AS Kind FROM crew c WHERE c.Species IN ('Moclan', 'Kaylon') ORDER BY c.Name", "Bortus/Moclan; Isaac/Kaylon")]
        [DataRow("SELECT c.Name, Species FROM crew c WHERE Species = 'Human' AND c.Rank < 5 ORDER BY c.Name DESC", "Kelly Grayson/Human; Gordon Malloy/Human")]
        public void AliasesQualifyASingleTable(string sql, string expected)
        {
            // Act
            var typed = Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = DictionaryTables().Query<Tuple<string, string>>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);
            var viaDictionaryQuery = Orville.CrewDictionaries().Query<IDictionary<string, object>, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
            Assert.AreEqual(expected, Orville.Rows(viaQuery));
            Assert.AreEqual(expected, Orville.Rows(viaDictionaryQuery));
        }

        [TestMethod]
        [DataRow("SELECT this.Name, this.Species FROM this WHERE this.Rank > 3", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT this.Name, Species FROM crew WHERE this.Rank > 3 ORDER BY this.Name DESC", "Kelly Grayson/Human; Ed Mercer/Human")]
        public void ThisQualifiesTheQuerySource(string sql, string expected)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = Orville.Crew() };

            // Act
            var typed = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, Tuple<string, string>>(sql);
            var viaTables = tables.Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
            Assert.AreEqual(expected, Orville.Rows(viaTables));
        }

        [TestMethod]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species ORDER BY c.Species", "Human/3; Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c WHERE c.Rank > 2 GROUP BY c.Species ORDER BY c.Species DESC", "Xelayan/1; Moclan/1; Human/2")]
        public void AliasesGroupASingleTable(string sql, string expected)
        {
            // Act
            var typed = Tables().Query<Tuple<string, int>>(sql);
            var dictionaries = DictionaryTables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        public void AliasedColumnsAreNamedByTheirField()
        {
            // Act
            var row = Tables().Query("SELECT c.Name, c.Species AS Kind FROM crew c WHERE c.Name = 'Bortus'").Single();
            var group = Tables().Query("SELECT c.Species, COUNT(*) AS Total FROM crew c GROUP BY c.Species ORDER BY c.Species").First();

            // Assert
            Assert.AreEqual("Bortus", row["Name"]);
            Assert.AreEqual("Moclan", row["Kind"]);
            Assert.AreEqual("Human", group["Species"]);
            Assert.AreEqual(3, group["Total"]);
        }

        [TestMethod]
        public void AliasesQualifyDynamicRows()
        {
            // Arrange
            const string Sql = "SELECT c.Name, c.Rank AS Grade FROM crew c WHERE c.Rank > 3 ORDER BY c.Name";
            var expandos = new List<object>();
            foreach (var member in Orville.Crew())
            {
                dynamic expando = new ExpandoObject();
                expando.Name = member.Name;
                expando.Rank = member.Rank;
                expandos.Add(expando);
            }

            IEnumerable<dynamic> crew = expandos;
            var tables = new Dictionary<string, IEnumerable> { ["crew"] = expandos };

            // Act
            var viaQuery = crew.Query<dynamic, dynamic>(Sql).ToList();
            var viaTables = tables.Query<dynamic>(Sql).ToList();

            // Assert
            Assert.HasCount(2, viaQuery);
            Assert.AreEqual("Ed Mercer", viaQuery[0].Name);
            Assert.AreEqual(5, viaQuery[0].Grade);
            Assert.HasCount(2, viaTables);
            Assert.AreEqual("Kelly Grayson", viaTables[1].Name);
            Assert.AreEqual(4, viaTables[1].Grade);
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > (SELECT AVG(Rank) FROM crew)", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId = (SELECT COUNT(*) FROM ships) - 1", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id <= (SELECT MAX(ShipId) FROM crew)", "Orville/Exploratory; Roosevelt/Exploratory")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id > (SELECT MIN(Id) FROM ships)", "Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank > (SELECT AVG(Rank) FROM crew)", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 5 AND (SELECT COUNT(*) FROM crew) = 6", "Ed Mercer/Orville")]
        public void SubqueriesReadTheirOwnTable(string sql, string expected)
        {
            // Act
            var result = Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void CompiledQuerySubqueriesReadTheEvaluatedSource()
        {
            // Arrange
            var query = new Query<CrewMember, string>("SELECT Name FROM crew WHERE Rank > (SELECT AVG(Rank) FROM ships)");
            query.Compile();

            // Act
            var humans = query.Evaluate(Orville.Crew().Where(c => c.Species == "Human"));
            var others = query.Evaluate(Orville.Crew().Where(c => c.Species != "Human"));

            // Assert
            Assert.AreEqual("Ed Mercer, Kelly Grayson", string.Join(", ", humans));
            Assert.AreEqual("Bortus, Talla Keyali", string.Join(", ", others));
        }

        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Pay > (SELECT AVG(c2.Pay) FROM crew c2 WHERE c2.Species = c.Species)", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank = (SELECT MAX(Rank) FROM crew WHERE Species = c.Species)", "Ed Mercer/Human; Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id) > 1", "Orville/Exploratory")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT COUNT(*) FROM ships s WHERE s.Id = c.ShipId AND s.Name = 'Roosevelt') = 1", "Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = (SELECT MAX(c2.Rank) FROM crew c2 WHERE c2.ShipId = s.Id)", "Ed Mercer/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id AND c.Rank > (SELECT AVG(c2.Rank) FROM crew c2 WHERE c2.ShipId = s.Id)) > 0", "Orville/Exploratory")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank > 3 AND (SELECT COUNT(*) FROM ships c WHERE c.Id = 1) = 1", "Ed Mercer/Human; Kelly Grayson/Human")]
        public void CorrelatedSubqueriesReadTheOuterRow(string sql, string expected)
        {
            // Act
            var result = Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT COUNT(*) FROM crew c2 WHERE c2.Species = c.Species) > 1", "Ed Mercer/Human; Kelly Grayson/Human; Gordon Malloy/Human")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id) > 1", "Orville/Exploratory")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE (SELECT COUNT(*) FROM crew c2 WHERE c2.ShipId = s.Id) = 1", "Talla Keyali/Roosevelt")]
        public void CorrelatedSubqueriesReadTypedAndDictionaryRows(string sql, string expected)
        {
            // Act
            var typed = Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId = (SELECT Id FROM ships WHERE Name = 'Roosevelt')", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Class FROM ships WHERE Name = (SELECT s.Name FROM ships s WHERE s.Id = 3)", "Pembroke/Science")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = (SELECT Id FROM ships WHERE Name = 'Enterprise')", "")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > 3 AND (SELECT Id FROM ships WHERE Name = 'Enterprise') IS NULL", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT c.Name FROM crew c WHERE c.ShipId = s.Id AND c.Rank = 5) IS NULL", "Roosevelt/Exploratory; Pembroke/Science")]
        public void SingleValueSubqueriesReturnTheirValueOrNull(string sql, string expected)
        {
            // Act
            var typed = Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > 1", "Human/3")]
        [DataRow("SELECT Species, MAX(Rank) FROM crew GROUP BY Species HAVING MAX(Rank) >= 3 ORDER BY Species", "Human/5; Moclan/3; Xelayan/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > 1 AND MIN(Age) > 30", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) >= 1 AND MIN(Age) > 30 ORDER BY Species", "Human/3; Kaylon/1; Moclan/1")]
        [DataRow("SELECT Species, MAX(Rank) FROM crew GROUP BY Species HAVING Species <> 'Human' AND MAX(Rank) = 3 ORDER BY Species", "Moclan/3; Xelayan/3")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c WHERE c.Rank < 5 GROUP BY c.Species HAVING COUNT(*) > 1", "Human/2")]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name HAVING COUNT(*) > 1", "Orville/4")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY c.Species HAVING Species <> 'Human' AND COUNT(*) = 1 ORDER BY c.Species", "Moclan/1; Xelayan/1")]
        public void HavingFiltersGroupsOnAggregates(string sql, string expected)
        {
            // Act
            var result = Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) FROM crew HAVING COUNT(*) > 5", "6")]
        [DataRow("SELECT COUNT(*) FROM crew HAVING COUNT(*) > 6", "")]
        [DataRow("SELECT MAX(Rank) FROM crew WHERE Species = 'Human' HAVING MIN(Age) > 30", "5")]
        public void HavingFiltersAnUngroupedAggregate(string sql, string expected)
        {
            // Act
            var typed = Tables().Query<int>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, int>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed));
            Assert.AreEqual(expected, string.Join(", ", viaQuery));
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id", AllPostings)]
        [DataRow("SELECT crew.Name, ships.Name AS Ship FROM crew, ships WHERE crew.ShipId = ships.Id AND ships.Name = 'Roosevelt'", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id ORDER BY s.Name DESC, c.Name", "Talla Keyali/Roosevelt; Bortus/Orville; Ed Mercer/Orville; Gordon Malloy/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT c.Name, k.Role FROM crew c, ships s JOIN classes k ON s.Class = k.Class WHERE c.ShipId = s.Id AND c.Rank > 3", "Ed Mercer/Exploration; Kelly Grayson/Exploration")]
        [DataRow("SELECT s.Name, k.Role FROM ships s, classes k WHERE s.Class = k.Class AND k.Role = 'Research'", "Pembroke/Research")]
        public void CommaJoinsCrossTheSourcesThenFilter(string sql, string expected)
        {
            // Act
            var typed = Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c, ships s GROUP BY s.Name ORDER BY s.Name", "Orville/6; Pembroke/6; Roosevelt/6")]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c, ships s WHERE c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Roosevelt/1")]
        public void CommaJoinsPairEveryRow(string sql, string expected)
        {
            // Act
            var result = Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void QueryKeepsIgnoringExtraFromItems()
        {
            // Act
            var result = Orville.Crew().Query("SELECT * FROM this, that WHERE Rank > 3");

            // Assert
            Assert.AreEqual("Ed Mercer, Kelly Grayson", string.Join(", ", result.Select(c => c.Name)));
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c WHERE x.Rank > 3", "x.Rank can't be read because x isn't a source or alias of this query")]
        [DataRow("SELECT c.Name FROM crew c WHERE c2.Rank > (SELECT AVG(c2.Rank) FROM crew c2)", "c2.Rank can't be read because c2 isn't a source or alias of this query")]
        [DataRow("SELECT Name FROM crew WHERE Rank > (SELECT AVG(Rank) FROM nowhere)", "'nowhere' can't be read because it isn't one of the sources")]
        [DataRow("SELECT Name FROM crew WHERE ShipId = (SELECT Id FROM ships WHERE Class = 'Exploratory')", "The subquery can't be used as a value because it returned more than one row.")]
        [DataRow("SELECT s.Name FROM ships s WHERE (SELECT c.Name FROM crew c WHERE c.ShipId = s.Id) IS NULL", "The subquery can't be used as a value because it returned more than one row.")]
        [DataRow("SELECT COUNT(*) FROM crew WHERE ShipId = (SELECT Id FROM ships)", "The subquery can't be used as a value because it returned more than one row.")]
        [DataRow("SELECT Name FROM ships WHERE Id = (SELECT COUNT(*) FROM crew WHERE ShipId = (SELECT Id FROM ships))", "The subquery can't be used as a value because it returned more than one row.")]
        [DataRow("SELECT Species, COUNT(*) FROM crew WHERE COUNT(*) > 1 GROUP BY Species", "WHERE can't use an aggregate because it filters rows before grouping")]
        [DataRow("SELECT * FROM crew c, ships c WHERE c.ShipId = c.Id", "joined more than once")]
        [DataRow("SELECT * FROM crew c, nowhere n", "'nowhere' can't be read because it isn't one of the sources")]
        [DataRow("SELECT s.Class, COUNT(*) FROM classes k, ships s GROUP BY Class", "The field 's.Class' can't be selected because it isn't in the GROUP BY clause.")]
        public void EngineReportsMistakesAsSqlException(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Tables().Query(sql).ToList());
            Assert.Contains(message, e.Message);
        }
    }
}
