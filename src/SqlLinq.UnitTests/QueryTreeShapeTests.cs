using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class QueryTreeShapeTests
    {
        [TestMethod]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank > 3 AND Species = 'Human' OR Age < 31")]
        [DataRow("SELECT Name, Rank FROM this WHERE NOT Rank > 2 AND ShipId IS NOT NULL")]
        [DataRow("SELECT Name, Rank FROM this WHERE Name LIKE 'E%' OR Name NOT LIKE '%a%'")]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank IN (2, 3) AND Rank NOT IN (5)")]
        [DataRow("SELECT Name, Rank FROM this WHERE Pay BETWEEN 2 AND 4 AND Age NOT BETWEEN 40 AND 50")]
        [DataRow("SELECT Name, Rank FROM this WHERE Name > 'K' ORDER BY Name DESC")]
        [DataRow("SELECT Name, Rank FROM this ORDER BY 2, 1")]
        [DataRow("SELECT Name, Rank * 10 + Age AS Score FROM this ORDER BY Score DESC")]
        [DataRow("SELECT Name, -Rank AS Down FROM this WHERE Age / 10 = 4")]
        [DataRow("SELECT DISTINCT Species, Rank FROM this ORDER BY Species")]
        [DataRow("SELECT Species, COUNT(*) FROM this GROUP BY Species HAVING COUNT(*) > 1")]
        [DataRow("SELECT Species, MAX(Rank) AS Top FROM this GROUP BY Species ORDER BY Top DESC, Species")]
        [DataRow("SELECT Species, SUM(Age) FROM this WHERE ShipId IS NOT NULL GROUP BY Species HAVING Species LIKE '%a%'")]
        [DataRow("SELECT Species, COUNT(DISTINCT Rank) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, COUNT(ShipId) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, MIN(Rank) - 1 AS Low FROM this GROUP BY Species")]
        [DataRow("SELECT MAX(Name), COUNT(*) FROM this")]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank > (SELECT AVG(Rank) FROM this)")]
        [DataRow("SELECT c.Name, c.Rank FROM this c WHERE c.Rank = (SELECT MAX(c2.Rank) FROM this c2 WHERE c2.Species = c.Species)")]
        [DataRow("SELECT Name, (SELECT COUNT(*) FROM this) AS Total FROM this")]
        [DataRow("SELECT c.Name, (SELECT COUNT(*) FROM this c2 WHERE c2.Species = c.Species) AS Kind FROM this c")]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank IN (SELECT Rank FROM this WHERE Species = 'Human')")]
        [DataRow("SELECT c.Name, c.Rank FROM this c WHERE c.Name NOT IN (SELECT d.Name FROM this d WHERE d.Rank > c.Rank)")]
        [DataRow("SELECT c.Name, c.Rank FROM this c WHERE EXISTS (SELECT * FROM this d WHERE d.Rank > c.Rank)")]
        [DataRow("SELECT c.Name, c.Rank FROM this c WHERE NOT EXISTS (SELECT * FROM this d WHERE d.Rank > c.Rank)")]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank > ALL (SELECT Rank FROM this WHERE Species = 'Moclan')")]
        [DataRow("SELECT c.Name, c.Rank FROM this c WHERE c.Rank >= ANY (SELECT d.Rank FROM this d WHERE d.Species = c.Species AND d.Name <> c.Name)")]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank > (SELECT AVG(Rank) FROM this WHERE Age > (SELECT MIN(Age) FROM this))")]
        [DataRow("SELECT Species, COUNT(*) FROM this GROUP BY Species HAVING COUNT(*) >= (SELECT MIN(Rank) FROM this)")]
        [DataRow("SELECT Name, Rank FROM this WHERE Rank BETWEEN (SELECT MIN(Rank) FROM this) AND 4")]
        public void TypedQueriesCompileToLinqShapes(string sql)
        {
            // Arrange
            var query = new Query<CrewMember, Tuple<string, int>>(sql);

            // Act
            query.Compile();
            var rows = query.Evaluate(Orville.Crew()).ToList();

            // Assert
            Assert.IsNotEmpty(rows);
            Assert.AreEqual("", TreeShape.Disallowed(query.Tree));
        }

        [TestMethod]
        [DataRow("SELECT Species, AVG(Rank) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, AVG(DISTINCT Pay) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, SUM(Pay) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, VAR(Age) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, VARP(Age) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, STDEV(Age) FROM this GROUP BY Species")]
        [DataRow("SELECT Species, STDEVP(Age) FROM this GROUP BY Species")]
        public void TypedAggregatesCompileToLinqShapes(string sql)
        {
            // Arrange
            var query = new Query<CrewMember, Tuple<string, double?>>(sql);

            // Act
            query.Compile();
            var rows = query.Evaluate(Orville.Crew()).ToList();

            // Assert
            Assert.IsNotEmpty(rows);
            Assert.AreEqual("", TreeShape.Disallowed(query.Tree));
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank > 2")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c INNER JOIN ships s ON s.Id = c.ShipId ORDER BY s.Name, c.Name")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id")]
        [DataRow("SELECT c.Name, p.Name AS Homeworld FROM crew c, planets p WHERE c.Species = p.Species")]
        [DataRow("SELECT c.Name, p.Name AS Homeworld FROM crew c JOIN ships s ON c.ShipId = s.Id LEFT JOIN planets p ON p.Species = c.Species WHERE s.Class = 'Exploratory'")]
        [DataRow("SELECT c.Name, d.Post FROM crew c JOIN duties d ON c = d.Officer")]
        [DataRow("SELECT a.Name, b.Name AS Peer FROM crew a JOIN crew b ON a.Rank = b.Rank WHERE a.Name <> b.Name")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id WHERE s.Name IS NULL OR s.Class LIKE 'E%'")]
        [DataRow("SELECT DISTINCT s.Name, s.Class FROM crew c JOIN ships s ON c.ShipId = s.Id")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = (SELECT MAX(Rank) FROM crew)")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id)")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > 3 UNION SELECT Name, Class FROM ships")]
        [DataRow("SELECT Name, Species FROM crew UNION ALL SELECT Name, Species FROM planets ORDER BY 1")]
        public void TypedJoinsAndUnionsCompileToLinqShapes(string sql)
        {
            // Arrange
            var tables = Orville.Tables();
            tables.Remove("classes");

            // Act
            var tree = SourcesExtensions.Tree<Tuple<string, string>>(tables, sql);
            var rows = tree.Compile()().ToList();

            // Assert
            Assert.IsNotEmpty(rows);
            Assert.AreEqual("", TreeShape.Disallowed(tree));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name")]
        [DataRow("SELECT s.Name, COUNT(c.Name) FROM ships s LEFT JOIN crew c ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name")]
        [DataRow("SELECT s.Name, COUNT(*) AS Aboard FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name HAVING COUNT(*) > 0 ORDER BY Aboard DESC")]
        [DataRow("SELECT p.Name, SUM(c.Age) FROM planets p JOIN crew c ON c.Species = p.Species GROUP BY p.Name")]
        public void TypedGroupedJoinsCompileToLinqShapes(string sql)
        {
            // Arrange
            var tables = Orville.Tables();
            tables.Remove("classes");

            // Act
            var tree = SourcesExtensions.Tree<Tuple<string, int>>(tables, sql);
            var rows = tree.Compile()().ToList();

            // Assert
            Assert.IsNotEmpty(rows);
            Assert.AreEqual("", TreeShape.Disallowed(tree));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > 3")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id")]
        public void UntypedQueriesKeepTheirHelpers(string sql)
        {
            // Act
            var tree = SourcesExtensions.Tree<Tuple<string, string>>(Orville.DictionaryTables(), sql);

            // Assert
            Assert.AreNotEqual("", TreeShape.Disallowed(tree));
        }
    }
}
