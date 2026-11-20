using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class MultiTableJoinTests
    {
        private const string AllPostings = "Ed Mercer/Orville; Kelly Grayson/Orville; Bortus/Orville; Gordon Malloy/Orville; Talla Keyali/Roosevelt";

        [TestMethod]
        [DataRow("SELECT c.Name, k.Role FROM crew c JOIN ships s ON s.Id = c.ShipId JOIN classes k ON k.Class = s.Class WHERE c.Species = 'Xelayan'", "Talla Keyali/Exploration")]
        [DataRow("SELECT s.Name, p.Name AS Homeworld FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN planets p ON c.Species = p.Species WHERE c.Rank = 3", "Orville/Moclus; Roosevelt/Xelaya")]
        [DataRow("SELECT c.Name, p.Name AS Homeworld FROM crew c INNER JOIN ships s ON c.ShipId = s.Id INNER JOIN classes k ON s.Class = k.Class INNER JOIN planets p ON p.Species = c.Species WHERE k.Role = 'Exploration' AND p.Moons > 1", "Talla Keyali/Xelaya")]
        [DataRow("SELECT c.Name, k.Role FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id LEFT JOIN classes k ON s.Class = k.Class WHERE c.Rank = 2", "Gordon Malloy/Exploration; Isaac/")]
        [DataRow("SELECT c.Name, k.Role FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id JOIN classes k ON s.Class = k.Class WHERE c.Rank = 2", "Gordon Malloy/Exploration")]
        [DataRow("SELECT s.Name, s.Class FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id WHERE c.Name IS NULL", "Pembroke/Science")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c RIGHT JOIN ships s ON s.Id = c.ShipId WHERE c.Name IS NULL", "/Pembroke")]
        [DataRow("SELECT s.Name, k.Role FROM crew c JOIN ships s ON c.ShipId = s.Id RIGHT JOIN classes k ON s.Class = k.Class", "Orville/Exploration; Orville/Exploration; Orville/Exploration; Orville/Exploration; Roosevelt/Exploration; /Research")]
        [DataRow("SELECT s.Name, k.Role FROM ships s LEFT JOIN classes k ON s.Class = k.Class ORDER BY s.Name DESC", "Roosevelt/Exploration; Pembroke/Research; Orville/Exploration")]
        [DataRow("SELECT p.Name, c.Name AS Member FROM planets p LEFT JOIN crew c ON c.Species = p.Species WHERE p.Moons < 2", "Earth/Ed Mercer; Earth/Kelly Grayson; Earth/Gordon Malloy; Moclus/Bortus")]
        [DataRow("SELECT k.Role, s.Name AS Ship FROM classes k JOIN ships s ON s.Class = k.Class", "Exploration/Orville; Exploration/Roosevelt; Research/Pembroke")]
        [DataRow("SELECT Name, Role FROM ships s JOIN classes k ON s.Class = k.Class WHERE Role = 'Research'", "Pembroke/Research")]
        [DataRow("SELECT a.Name, b.Name AS Peer FROM crew a JOIN crew b ON b.Rank = a.Rank WHERE a.Name <> b.Name ORDER BY a.Name", "Bortus/Talla Keyali; Gordon Malloy/Isaac; Isaac/Gordon Malloy; Talla Keyali/Bortus")]
        [DataRow("SELECT a.Name, b.Name AS Peer FROM crew a JOIN crew b ON a.Rank = b.Rank WHERE a.Rank = 3", "Bortus/Bortus; Bortus/Talla Keyali; Talla Keyali/Bortus; Talla Keyali/Talla Keyali")]
        [DataRow("SELECT a.Name, b.Name AS Crewmate FROM crew a JOIN crew b ON a.ShipId = b.ShipId WHERE a.Name = 'Bortus' AND b.Name <> 'Bortus'", "Bortus/Ed Mercer; Bortus/Kelly Grayson; Bortus/Gordon Malloy")]
        [DataRow("SELECT a.Name, b.Name AS Crewmate FROM crew a LEFT JOIN crew b ON a.ShipId = b.ShipId WHERE a.Name = 'Isaac'", "Isaac/")]
        [DataRow("SELECT a.Name, b.Name AS Peer FROM crew a JOIN crew b ON a.Species = b.Species WHERE a.Name <> b.Name AND a.Rank = 5", "Ed Mercer/Kelly Grayson; Ed Mercer/Gordon Malloy")]
        [DataRow("SELECT a.Name, s.Name AS Ship FROM crew a JOIN crew b ON a.Species = b.Species JOIN ships s ON b.ShipId = s.Id WHERE a.Species = 'Xelayan'", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Name LIKE 'R%' OR c.Age BETWEEN 44 AND 46", "Bortus/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Species IN ('Moclan', 'Xelayan') AND s.Class = 'Exploratory'", "Bortus/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Pay > 4", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE NOT s.Name = 'Orville' OR c.Rank = 5", "Ed Mercer/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY c.Age", "Talla Keyali/Roosevelt; Gordon Malloy/Orville; Kelly Grayson/Orville; Ed Mercer/Orville; Bortus/Orville")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY c.Pay DESC", "Ed Mercer/Orville; Kelly Grayson/Orville; Talla Keyali/Roosevelt; Bortus/Orville; Gordon Malloy/Orville")]
        [DataRow("SELECT C.Name, S.Name AS Ship FROM crew c JOIN ships s ON C.ShipId = S.ID WHERE S.Id = 2", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT DISTINCT s.Class, k.Role FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes k ON s.Class = k.Class", "Exploratory/Exploration")]
        [DataRow("SELECT DISTINCT c.Species, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY s.Name DESC, c.Species", "Xelayan/Roosevelt; Human/Orville; Moclan/Orville")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank * 10 = c.Age", "Kelly Grayson/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, p.Name AS Planet FROM ships s JOIN crew c ON s.Id = c.ShipId JOIN planets p ON Rank = p.Moons", "Bortus/Kaylon 1; Gordon Malloy/Xelaya; Talla Keyali/Kaylon 1")]
        public void JoinsSelectTheExpectedPairs(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name, k.Role FROM crew c, ships s, classes k WHERE c.ShipId = s.Id AND s.Class = k.Class AND c.Rank >= 4", "Ed Mercer/Exploration; Kelly Grayson/Exploration")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id AND (s.Id = 2 OR c.Rank = 5)", "Ed Mercer/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT a.Name, b.Name AS Peer FROM crew a, crew b WHERE a.Rank = b.Rank AND a.Name <> b.Name AND a.Species = 'Moclan'", "Bortus/Talla Keyali")]
        [DataRow("SELECT c.Name, p.Name AS Homeworld FROM crew c, planets p WHERE c.Species = p.Species AND c.ShipId IS NULL", "Isaac/Kaylon 1")]
        [DataRow("SELECT p.Name, k.Role FROM planets p, ships s JOIN classes k ON k.Class = s.Class WHERE s.Name = 'Pembroke' AND p.Moons = 0", "Moclus/Research")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id AND s.Class = 'Exploratory' AND c.Species <> 'Human' ORDER BY c.Name DESC", "Talla Keyali/Roosevelt; Bortus/Orville")]
        [DataRow("SELECT s.Name, k.Role FROM ships s, classes k WHERE s.Class <> k.Class ORDER BY s.Name", "Orville/Research; Pembroke/Exploration; Roosevelt/Research")]
        public void CommaJoinsSelectTheExpectedPairs(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name AS Ship, k.Role FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes k ON s.Class = k.Class WHERE c.Rank = 3", "Bortus/Orville/Exploration; Talla Keyali/Roosevelt/Exploration")]
        [DataRow("SELECT c.Name, s.Name AS Ship, p.Name AS Homeworld FROM crew c, ships s, planets p WHERE c.ShipId = s.Id AND c.Species = p.Species AND p.Moons = 0", "Bortus/Orville/Moclus")]
        [DataRow("SELECT c.Name, s.Name AS Ship, k.Role FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id LEFT JOIN classes k ON s.Class = k.Class WHERE c.Age > 100", "Isaac//")]
        public void JoinsSelectThreeColumnsByPosition(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Name = 'Orville' GROUP BY c.Species HAVING COUNT(*) < 3 ORDER BY c.Species DESC", "Moclan/1")]
        [DataRow("SELECT s.Name, COUNT(*) FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Pembroke/1; Roosevelt/1")]
        [DataRow("SELECT k.Role, COUNT(*) FROM classes k LEFT JOIN ships s ON s.Class = k.Class LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY k.Role ORDER BY k.Role", "Exploration/5; Research/1")]
        [DataRow("SELECT p.Name, COUNT(*) FROM crew c JOIN planets p ON c.Species = p.Species GROUP BY p.Name HAVING COUNT(*) = 1 ORDER BY p.Name", "Kaylon 1/1; Moclus/1; Xelaya/1")]
        [DataRow("SELECT k.Role, COUNT(*) FROM crew c, ships s, classes k WHERE c.ShipId = s.Id AND s.Class = k.Class GROUP BY k.Role", "Exploration/5")]
        [DataRow("SELECT a.Name, COUNT(*) FROM crew a JOIN crew b ON a.Species = b.Species WHERE a.Name <> b.Name GROUP BY a.Name ORDER BY a.Name", "Ed Mercer/2; Gordon Malloy/2; Kelly Grayson/2")]
        [DataRow("SELECT s.Class, COUNT(*) FROM crew c, ships s GROUP BY s.Class ORDER BY s.Class", "Exploratory/12; Science/6")]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Species = 'Human' GROUP BY s.Name HAVING COUNT(*) > 1", "Orville/3")]
        [DataRow("SELECT s.Class, COUNT(DISTINCT c.Species) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Class", "Exploratory/3")]
        [DataRow("SELECT s.Name, COUNT(ALL c.Species) FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Pembroke/0; Roosevelt/1")]
        public void JoinsGroupTheExpectedTotals(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, int>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name, d.Post FROM crew c JOIN duties d ON c = d.Officer", "Ed Mercer/Captain; Kelly Grayson/First Officer; Bortus/Second Officer; Bortus/Weapons; Gordon Malloy/Helm; Talla Keyali/Security")]
        [DataRow("SELECT c.Name, d.Post FROM crew c JOIN duties d ON d.Officer = c WHERE c.Rank = 3", "Bortus/Second Officer; Bortus/Weapons; Talla Keyali/Security")]
        [DataRow("SELECT c.Name, d.Post FROM crew c LEFT JOIN duties d ON c = d.Officer WHERE d.Post IS NULL", "Isaac/")]
        [DataRow("SELECT c.Name, d.Post FROM crew c RIGHT JOIN duties d ON c = d.Officer WHERE c.Name IS NULL", "/Navigation")]
        [DataRow("SELECT d.Post, s.Name AS Ship FROM duties d JOIN crew c ON d.Officer = c JOIN ships s ON c.ShipId = s.Id WHERE s.Id = 2", "Security/Roosevelt")]
        public void WholeItemKeysJoinTheSameObject(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void WholeItemKeysGroupDuplicateMatches()
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>("SELECT c.Name, COUNT(*) FROM crew c JOIN duties d ON c = d.Officer GROUP BY c.Name HAVING COUNT(*) > 1");

            // Assert
            Assert.AreEqual("Bortus/2", Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT this.Name, that.Post FROM this INNER JOIN that ON this = that.Officer WHERE this.Rank = 3", "Bortus/Second Officer; Bortus/Weapons; Talla Keyali/Security")]
        [DataRow("SELECT this.Name, that.Post FROM this LEFT JOIN that ON that.Officer = this WHERE this.Rank = 2", "Gordon Malloy/Helm; Isaac/")]
        [DataRow("SELECT this.Name, that.Post FROM this RIGHT JOIN that ON this = that.Officer WHERE this.Name IS NULL", "/Navigation")]
        public void ThisAndThatJoinWholeItems(string sql, string expected)
        {
            // Arrange
            var crew = Orville.Crew();

            // Act
            var result = crew.Query<CrewMember, Duty, Tuple<string, string>>(sql, Orville.Duties(crew));

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this LEFT JOIN that ON this.ShipId = that.Id", AllPostings + "; Isaac/")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this RIGHT JOIN that ON that.Id = this.ShipId", AllPostings + "; /Pembroke")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this JOIN that ON this.ShipId = that.Id WHERE that.Id = 2", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id WHERE this.Pay > 4", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM this c JOIN that s ON c.ShipId = s.Id WHERE s.Name = 'Roosevelt'", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id WHERE that.Name IN ('Roosevelt', 'Pembroke') OR this.Name LIKE 'B%'", "Bortus/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this LEFT JOIN that ON this.ShipId = that.Id WHERE that.Id IS NULL", "Isaac/")]
        public void ThisAndThatJoinEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>(sql, Orville.Ships());
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, IDictionary<string, object>, Tuple<string, string>>(sql, Orville.ShipDictionaries());
            var expandos = Orville.CrewExpandos().Query<dynamic, Ship, Tuple<string, string>>(sql, Orville.Ships());

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionaries");
            Assert.AreEqual(expected, Orville.Rows(expandos), "expandos");
        }

        [TestMethod]
        [DataRow("SELECT that.Name, COUNT(*) FROM this RIGHT JOIN that ON this.ShipId = that.Id GROUP BY that.Name ORDER BY that.Name", "Orville/4; Pembroke/1; Roosevelt/1")]
        [DataRow("SELECT that.Class, COUNT(*) FROM this JOIN that ON this.ShipId = that.Id WHERE this.Rank < 5 GROUP BY that.Class HAVING COUNT(*) > 3", "Exploratory/4")]
        public void ThisAndThatGroupTheExpectedTotals(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, Ship, Tuple<string, int>>(sql, Orville.Ships());
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, IDictionary<string, object>, Tuple<string, int>>(sql, Orville.ShipDictionaries());

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionaries");
        }

        [TestMethod]
        public void JoinsMixRowShapesAcrossTables()
        {
            // Arrange
            const string Sql = "SELECT c.Name, k.Role FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes k ON s.Class = k.Class WHERE c.Rank = 4";
            var typedCrew = new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.Crew(),
                ["ships"] = Orville.ShipDictionaries(),
                ["classes"] = Orville.Classes(),
            };
            var expandoCrew = new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.CrewExpandos(),
                ["ships"] = Orville.Ships(),
                ["classes"] = Orville.Classes(),
            };

            // Act
            var typed = typedCrew.Query<Tuple<string, string>>(Sql);
            var expandos = expandoCrew.Query<Tuple<string, string>>(Sql);

            // Assert
            Assert.AreEqual("Kelly Grayson/Exploration", Orville.Rows(typed));
            Assert.AreEqual("Kelly Grayson/Exploration", Orville.Rows(expandos));
        }

        [TestMethod]
        public void InnerRowsMatchEveryDuplicateKey()
        {
            // Arrange
            var ships = Orville.Ships();
            ships.Add(new Ship { Id = 2, Name = "Roosevelt Shuttle", Class = "Shuttle" });
            var tables = new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.Crew(),
                ["ships"] = ships,
                ["classes"] = Orville.Classes(),
            };

            // Act
            var inner = tables.Query<Tuple<string, string>>("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 3");
            var left = tables.Query<Tuple<string, string>>("SELECT s.Name, k.Role FROM ships s LEFT JOIN classes k ON s.Class = k.Class WHERE s.Id = 2");

            // Assert
            Assert.AreEqual("Bortus/Orville; Talla Keyali/Roosevelt; Talla Keyali/Roosevelt Shuttle", Orville.Rows(inner));
            Assert.AreEqual("Roosevelt/Exploration; Roosevelt Shuttle/", Orville.Rows(left));
        }

        [TestMethod]
        [DataRow("c.ShipId = s.Id")]
        [DataRow("c.[ShipId] = s.[Id]")]
        [DataRow("[c].[ShipId] = [s].[Id]")]
        [DataRow("[c].ShipId = [s].Id")]
        [DataRow("s.Id = c.ShipId")]
        public void BracketedJoinKeysJoinLikePlainOnes(string on)
        {
            // Arrange
            var sql = "SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON " + on + " WHERE c.Rank = 3";

            // Act
            var typed = Orville.Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual("Bortus/Orville; Talla Keyali/Roosevelt", Orville.Rows(typed));
            Assert.AreEqual("Bortus/Orville; Talla Keyali/Roosevelt", Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("c.ShipId = s.[Ship.Id]")]
        [DataRow("s.[Ship.Id] = c.[ShipId]")]
        public void BracketedJoinKeysCanHoldADot(string on)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.CrewDictionaries(),
                ["ships"] = Orville.ShipDictionaries().Select(s => (IDictionary<string, object>)new Dictionary<string, object> { ["Ship.Id"] = s["Id"], ["Name"] = s["Name"] }).ToList(),
            };

            // Act
            var result = tables.Query<Tuple<string, string>>("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON " + on + " WHERE c.Rank = 3");

            // Assert
            Assert.AreEqual("Bortus/Orville; Talla Keyali/Roosevelt", Orville.Rows(result));
        }
    }
}
