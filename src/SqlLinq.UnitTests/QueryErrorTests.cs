using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class QueryErrorTests
    {
        [TestMethod]
        [DataRow("SELEC Name FROM crew")]
        [DataRow("SELECT Name FROM")]
        [DataRow("SELECT Name, FROM crew")]
        [DataRow("SELECT Name FROM crew c d")]
        [DataRow("SELECT Name FROM crew WHERE Rank =")]
        [DataRow("SELECT Name FROM crew WHERE (Rank = 3")]
        [DataRow("SELECT Name FROM crew WHERE Rank BETWEEN 2")]
        [DataRow("SELECT Name FROM crew WHERE Rank IN ()")]
        [DataRow("SELECT Name FROM crew WHERE Name LIKE")]
        [DataRow("SELECT Name FROM crew WHERE Name = 'Ed")]
        [DataRow("SELECT Name FROM crew WHERE Rank = (SELECT MAX(Rank) FROM crew")]
        [DataRow("SELECT Species FROM crew GROUP Species")]
        [DataRow("SELECT Name FROM crew ORDER BY Name DESC ASC")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN")]
        [DataRow("SELECT c.Name FROM crew c LEFT ships s ON c.ShipId = s.Id")]
        public void ParseErrorsThrowSqlException(string sql)
        {
            // Act
            var viaTables = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            var viaQuery = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query(sql).ToList());
            var viaJoin = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>(sql, Orville.Ships()).ToList());

            // Assert
            Assert.StartsWith("SQL parse error", viaTables.Message);
            Assert.StartsWith("SQL parse error", viaQuery.Message);
            Assert.StartsWith("SQL parse error", viaJoin.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM nowhere", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT * FROM nowhere n JOIN ships s ON n.ShipId = s.Id", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT * FROM crew c, ships s, nowhere n WHERE c.ShipId = s.Id", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT c.Name FROM crew c WHERE (SELECT COUNT(*) FROM nowhere n WHERE n.Id = c.ShipId) = 0", "'nowhere' isn't one of the sources")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY x.Species", "x.Species")]
        [DataRow("SELECT * FROM crew c JOIN ships s ON s.Id = s.Id", "must compare a field of the joined source with a field of an earlier source")]
        [DataRow("SELECT * FROM crew c JOIN ships s ON c.ShipId = k.Id JOIN classes k ON s.Class = k.Class", "must compare a field of the joined source with a field of an earlier source")]
        [DataRow("SELECT * FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes k ON c.Species = s.Class", "must compare a field of the joined source with a field of an earlier source")]
        [DataRow("SELECT * FROM crew c JOIN ships C ON c.ShipId = C.Id", "joined more than once")]
        [DataRow("SELECT * FROM crew JOIN crew ON crew.Rank = crew.Rank", "joined more than once")]
        [DataRow("SELECT * FROM crew c, ships s, crew c WHERE c.ShipId = s.Id", "joined more than once")]
        [DataRow("SELECT * FROM crew c JOIN ships s ON c.ShipId = s.Id JOIN classes s ON s.Class = s.Class", "joined more than once")]
        [DataRow("SELECT c.Name FROM crew c WHERE c.Rank = (SELECT Rank FROM crew WHERE Species = 'Human')", "The subquery returned more than one row.")]
        [DataRow("SELECT s.Name FROM ships s WHERE s.Id = (SELECT c.ShipId FROM crew c WHERE c.Rank = 3)", "The subquery returned more than one row.")]
        [DataRow("SELECT s.Name FROM ships s WHERE (SELECT c.Name FROM crew c WHERE c.ShipId = s.Id) = 'Bortus'", "The subquery returned more than one row.")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) = (SELECT Id FROM ships)", "The subquery returned more than one row.")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Class = (SELECT Class FROM ships)", "The subquery returned more than one row.")]
        [DataRow("SELECT c.Name FROM crew c, ships s WHERE c.ShipId = s.Id AND s.Name = (SELECT Name FROM ships WHERE Class = 'Exploratory')", "The subquery returned more than one row.")]
        [DataRow("SELECT s.Name, COUNT(*) FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE COUNT(*) > 1 GROUP BY s.Name", "Aggregate expressions are not valid in a WHERE clause")]
        public void TableMistakesThrowSqlException(string sql, string message)
        {
            // Act
            var typed = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());
            var dictionaries = Assert.ThrowsExactly<SqlException>(() => Orville.DictionaryTables().Query(sql).ToList());

            // Assert
            Assert.Contains(message, typed.Message);
            Assert.Contains(message, dictionaries.Message);
        }

        [TestMethod]
        [DataRow("SELECT * FROM crew WHERE [<Name>k__BackingField] = 'Ed Mercer'")]
        [DataRow("SELECT [<Name>k__BackingField] AS Name FROM crew")]
        [DataRow("SELECT Name FROM crew ORDER BY [<Name>k__BackingField]")]
        [DataRow("SELECT MAX([<Name>k__BackingField]) AS Last FROM crew")]
        [DataRow("SELECT [<Name>k__BackingField] AS Name, COUNT(*) AS Members FROM crew GROUP BY [<Name>k__BackingField]")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.[<Name>k__BackingField] = 'Ed Mercer'")]
        public void NonPublicMembersFailLikeMissingOnes(string sql)
        {
            // Arrange
            var missing = sql.Replace("[<Name>k__BackingField]", "[Nowhere]", StringComparison.Ordinal);

            // Act
            var hidden = Assert.Throws<Exception>(() => Orville.Tables().Query(sql).ToList());
            var absent = Assert.Throws<Exception>(() => Orville.Tables().Query(missing).ToList());

            // Assert
            Assert.AreEqual(absent.GetType(), hidden.GetType());
            Assert.AreEqual(absent.Message, hidden.Message.Replace("<Name>k__BackingField", "Nowhere", StringComparison.Ordinal));
        }

        [TestMethod]
        [DataRow("(", "Rank = 1", ")")]
        [DataRow("Rank = 1 AND ", "Rank = 1", "")]
        [DataRow("Rank + ", "Rank > 0", "")]
        [DataRow("Rank IN (SELECT Rank FROM this WHERE ", "Rank = 1", ")")]
        public void DeeplyNestedQueriesThrowSqlException(string open, string inner, string close)
        {
            // Arrange
            var sql = "SELECT * FROM this WHERE " + string.Concat(Enumerable.Repeat(open, 20000)) + inner + string.Concat(Enumerable.Repeat(close, 20000));
            SqlException thrown = null;
            var thread = new Thread(
                () =>
                {
                    try
                    {
                        _ = Orville.Crew().Query(sql).ToList();
                    }
                    catch (SqlException e)
                    {
                        thrown = e;
                    }
                },
                1024 * 1024);

            // Act
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("The query is nested too deeply.", thrown?.Message);
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c WHERE (SELECT COUNT(*) FROM ships s WHERE s.Id = x.ShipId) > 0", "x in x.ShipId isn't a source or alias of this query")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank > 3 AND nothing.Rank IS NULL", "nothing in nothing.Rank isn't a source or alias of this query")]
        public void UnknownAliasesInTypedWhereThrowSqlException(string sql, string message)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());

            // Assert
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT * FROM this t WHERE x.Rank > 3", "x in x.Rank isn't a source or alias of this query")]
        [DataRow("SELECT * FROM this WHERE that.Rank > 3", "that in that.Rank isn't a source or alias of this query")]
        [DataRow("SELECT * FROM this WHERE Rank > 3 AND nothing.Rank IS NULL", "nothing in nothing.Rank isn't a source or alias of this query")]
        [DataRow("SELECT * FROM this WHERE Rank = (SELECT Rank FROM this WHERE Species = 'Human')", "The subquery returned more than one row.")]
        [DataRow("SELECT * FROM this WHERE COUNT(*) > 1", "Aggregate expressions are not valid in a WHERE clause")]
        public void QueryMistakesThrowSqlException(string sql, string message)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query(sql).ToList());

            // Assert
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank = (SELECT Rank FROM this WHERE Species = 'Human')", "The subquery returned more than one row.")]
        [DataRow("SELECT * FROM this WHERE COUNT(*) > 1", "Aggregate expressions are not valid in a WHERE clause")]
        public void DictionaryQueryMistakesThrowSqlException(string sql, string message)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.CrewDictionaries().Query(sql).ToList());

            // Assert
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE Rank > 3", "The query does not contain a JOIN expression")]
        [DataRow("SELECT Name FROM this, that WHERE this.ShipId = that.Id", "The query does not contain a JOIN expression")]
        [DataRow("SELECT Name FROM this JOIN that ON this.ShipId = this.Rank", "must compare a field of the joined source with a field of an earlier source")]
        [DataRow("SELECT Name, that.Name AS Ship FROM this JOIN that ON this.ShipId = that.Id WHERE Rank = (SELECT Id FROM that)", "The subquery returned more than one row.")]
        public void JoinQueryMistakesThrowSqlException(string sql, string message)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>(sql, Orville.Ships()).ToList());

            // Assert
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) FROM this WHERE")]
        [DataRow("SELECT MAX(Rank) FROM this WHERE Rank = (SELECT Rank FROM this WHERE Species = 'Human')")]
        public void ScalarMistakesThrowSqlException(string sql)
        {
            // Act & Assert
            Assert.ThrowsExactly<SqlException>(() => Orville.Crew().QueryScalar<CrewMember, int>(sql));
        }
    }
}
