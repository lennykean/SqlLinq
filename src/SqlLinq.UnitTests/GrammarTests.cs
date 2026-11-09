using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class GrammarTests
    {
        [TestMethod]
        [DataRow("SELECT Pay * 2 AS Twice FROM this")]
        [DataRow("SELECT MAX(Pay) - MIN(Pay) AS Spread FROM this")]
        [DataRow("SELECT Name AS N, Rank + 1 AS Next FROM this")]
        [DataRow("SELECT Name FROM this UNION SELECT Name FROM that")]
        [DataRow("SELECT Name FROM this UNION ALL SELECT Name FROM that UNION SELECT Name FROM other")]
        [DataRow("SELECT * FROM crew c WHERE EXISTS (SELECT * FROM ships s WHERE s.Id = c.ShipId)")]
        [DataRow("SELECT * FROM crew c WHERE NOT EXISTS (SELECT * FROM ships s WHERE s.Id = c.ShipId)")]
        [DataRow("SELECT * FROM crew WHERE ShipId IN (SELECT Id FROM ships WHERE Class = 'Science')")]
        [DataRow("SELECT * FROM crew WHERE ShipId NOT IN (SELECT Id FROM ships)")]
        [DataRow("SELECT * FROM crew WHERE Pay > ALL (SELECT Pay FROM crew WHERE Rank < 3)")]
        [DataRow("SELECT * FROM crew WHERE Pay = ANY (SELECT Pay FROM crew)")]
        [DataRow("SELECT * FROM crew WHERE Pay <> SOME (SELECT Pay FROM crew)")]
        [DataRow("SELECT SUM(DISTINCT Pay) AS S, AVG(DISTINCT Rank) AS A, MIN(DISTINCT Age) AS M, MAX(DISTINCT Age) AS X FROM this")]
        [DataRow("SELECT Name, Rank FROM this ORDER BY 2 DESC, 1")]
        [DataRow("SELECT * FROM this WHERE Name LIKE 'a!%%' ESCAPE '!'")]
        [DataRow("SELECT * FROM this WHERE Name NOT LIKE 'a!%%' ESCAPE '!'")]
        [DataRow("SELECT * FROM this WHERE Pay > 1.5E3 OR Pay < 2e-2 OR Pay = 3E1")]
        [DataRow("SELECT * FROM crew AS c JOIN ships AS s ON c.ShipId = s.Id")]
        [DataRow("SELECT * FROM this WHERE age IN ((SELECT MAX(age) FROM this))")]
        [DataRow("SELECT Name AS N FROM this")]
        [DataRow("SELECT MAX(Pay) AS M FROM this")]
        public void ExtendedSyntaxParses(string sql)
        {
            // Arrange
            var parser = new SqlParser();

            // Act
            var parsed = parser.Parse(sql);

            // Assert
            Assert.IsTrue(parsed, parser.ErrorString);
        }
    }
}
