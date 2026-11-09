using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class CancellationTests
    {
        [TestMethod]
        public void CancelledTokenStopsTheQueryBeforeItCompiles()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // Act
            var exception = Assert.ThrowsExactly<OperationCanceledException>(() => Orville.Crew().Query("SELECT * FROM this", cancellation.Token));

            // Assert
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }

        [TestMethod]
        public void CancellingStopsATypedQueryWhileItReadsRows()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var rows = CancelOnRead(Orville.Crew(), 2, cancellation).Query("SELECT * FROM this WHERE Rank > 2", cancellation.Token);

            // Act
            var exception = Assert.ThrowsExactly<OperationCanceledException>(() => rows.ToList());

            // Assert
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank > 2")]
        [DataRow("SELECT Species, COUNT(*) AS Members FROM crew GROUP BY Species")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id")]
        [DataRow("SELECT s.Name FROM ships s LEFT JOIN crew c ON s.Id = c.ShipId")]
        [DataRow("SELECT Name FROM ships WHERE Id IN (SELECT ShipId FROM crew)")]
        [DataRow("SELECT Name FROM ships s WHERE EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id)")]
        public void CancellingStopsATablesQueryWhileItReadsRows(string sql)
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var tables = Orville.Tables();
            tables["crew"] = CancelOnRead(Orville.Crew(), 2, cancellation);
            var rows = tables.Query(sql, cancellation.Token);

            // Act
            var exception = Assert.ThrowsExactly<OperationCanceledException>(() => rows.ToList());

            // Assert
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }

        [TestMethod]
        public void CancellingIsNotSwallowedByComparisonsOnDictionaryRows()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var tables = Orville.DictionaryTables();
            tables["ships"] = Orville.ShipDictionaries().Take(1).ToList();
            tables["crew"] = CancelOnRead(Orville.CrewDictionaries(), 0, cancellation);
            var rows = tables.Query("SELECT Name FROM ships s WHERE s.Id = (SELECT MAX(c.ShipId) FROM crew c WHERE c.ShipId = s.Id)", cancellation.Token);

            // Act
            var exception = Assert.ThrowsExactly<OperationCanceledException>(() => rows.ToList());

            // Assert
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }

        [TestMethod]
        [Timeout(10000)]
        public void TimeoutStopsARunawayQuery()
        {
            // Arrange
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var rows = Endless().Query("SELECT * FROM this WHERE Rank > 10", timeout.Token);

            // Act
            var exception = Assert.ThrowsExactly<OperationCanceledException>(() => rows.ToList());

            // Assert
            Assert.AreEqual(timeout.Token, exception.CancellationToken);
        }

        [TestMethod]
        public void TokenThatIsNeverCancelledGivesTheSameRows()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var sql = "SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY c.Name";

            // Act
            var rows = Orville.Tables().Query(sql, cancellation.Token).ToList();

            // Assert
            CollectionAssert.AreEqual(
                Orville.Tables().Query(sql).Select(r => r["Name"] + "/" + r["Ship"]).ToList(),
                rows.Select(r => r["Name"] + "/" + r["Ship"]).ToList());
        }

        private static IEnumerable<T> CancelOnRead<T>(IEnumerable<T> rows, int read, CancellationTokenSource cancellation)
        {
            foreach (var row in rows)
            {
                if (read-- == 0)
                {
                    cancellation.Cancel();
                }

                yield return row;
            }
        }

        private static IEnumerable<CrewMember> Endless()
        {
            while (true)
            {
                yield return new CrewMember { Name = "Ensign", Rank = 1 };
            }
        }
    }
}
