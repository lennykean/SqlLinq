using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class TablesTests
    {
        private const string AllPostings = "Ed Mercer/Orville; Kelly Grayson/Orville; Bortus/Orville; Gordon Malloy/Orville; Talla Keyali/Roosevelt";

        private static Dictionary<string, IEnumerable> Tables()
        {
            return new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.Crew(),
                ["ships"] = Orville.Ships(),
                ["classes"] = new List<IDictionary<string, object>>
                {
                    new Dictionary<string, object> { ["Class"] = "Exploratory", ["Role"] = "Exploration" },
                    new Dictionary<string, object> { ["Class"] = "Science", ["Role"] = "Research" },
                },
            };
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id", AllPostings)]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c INNER JOIN ships s ON s.Id = c.ShipId", AllPostings)]
        [DataRow("SELECT crew.Name, ships.Name AS Ship FROM crew INNER JOIN ships ON crew.ShipId = ships.Id", AllPostings)]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c LEFT JOIN ships s ON c.ShipId = s.Id", AllPostings + "; Isaac/")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id", AllPostings + "; /Pembroke")]
        [DataRow("SELECT c.Name, k.Role FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes k ON s.Class = k.Class WHERE c.Rank > 3", "Ed Mercer/Exploration; Kelly Grayson/Exploration")]
        [DataRow("SELECT s.Name, k.Role FROM ships s RIGHT JOIN classes k ON s.Class = k.Class ORDER BY s.Name", "Orville/Exploration; Pembroke/Research; Roosevelt/Exploration")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Name = 'Roosevelt'", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY s.Name DESC, c.Name", "Talla Keyali/Roosevelt; Bortus/Orville; Ed Mercer/Orville; Gordon Malloy/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id > 1", "Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT Class, Role FROM classes", "Exploratory/Exploration; Science/Research")]
        [DataRow("SELECT Name, Class FROM ships -- JOIN crew c ON c.ShipId = ships.Id\nWHERE Id > 1", "Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT Name, Class FROM ships /* JOIN crew c ON c.ShipId = ships.Id WHERE */ WHERE Name <> 'JOIN crew ON x = y'", "Orville/Exploratory; Roosevelt/Exploratory; Pembroke/Science")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c /* LEFT */ JOIN ships s ON c.ShipId = s.Id -- RIGHT JOIN classes k ON s.Class = k.Class", AllPostings)]
        public void TablesSelectTheExpectedRows(string sql, string expected)
        {
            // Act
            var result = Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Roosevelt/1")]
        [DataRow("SELECT k.Role, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes k ON s.Class = k.Class GROUP BY k.Role", "Exploration/5")]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/4; Pembroke/1; Roosevelt/1")]
        public void TablesGroupTheExpectedTotals(string sql, string expected)
        {
            // Act
            var result = Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT * FROM crew c JOIN nowhere n ON c.ShipId = n.Id", "'nowhere' can't be read because it isn't one of the sources")]
        [DataRow("SELECT * FROM crew c JOIN ships s ON c.ShipId = c.Rank", "doesn't compare a field of the joined source with a field of an earlier source")]
        [DataRow("SELECT * FROM crew c JOIN ships c ON c.ShipId = c.Id", "joined more than once")]
        public void TablesReportMistakesAsSqlException(string sql, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Tables().Query(sql));
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        public void OneTableBehavesLikeQuery()
        {
            // Arrange
            var crew = Orville.Crew();
            var tables = new Dictionary<string, IEnumerable> { ["this"] = crew };

            // Act
            var viaTables = tables.Query<CrewMember>("SELECT * FROM this WHERE Age / 2 = 20").ToList();
            var viaQuery = crew.Query("SELECT * FROM this WHERE Age / 2 = 20").ToList();

            // Assert
            CollectionAssert.AreEqual(viaQuery, viaTables);
            Assert.AreSame(crew[0], viaTables[0]);
        }

        [TestMethod]
        public void ThisAndThatBehaveLikeTheJoinQuery()
        {
            // Arrange
            const string Sql = "SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id";
            var tables = new Dictionary<string, IEnumerable> { ["this"] = Orville.Crew(), ["that"] = Orville.Ships() };

            // Act
            var viaTables = tables.Query<Tuple<string, string>>(Sql);
            var viaJoin = Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>(Sql, Orville.Ships());

            // Assert
            Assert.AreEqual(Orville.Rows(viaJoin), Orville.Rows(viaTables));
        }
    }
}
