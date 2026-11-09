using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class HavingAggregateTests
    {
        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING MIN(Age) < 35 ORDER BY Species", "Human/3; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING AVG(Age) > 40 ORDER BY Species", "Kaylon/1; Moclan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING AVG(Age) BETWEEN 38 AND 39", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING AVG(Rank) > 3", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > 1 OR MAX(Age) > 100 ORDER BY Species", "Human/3; Kaylon/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING MAX(Age) = 200 OR MIN(Age) = 30 ORDER BY Species", "Kaylon/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING (COUNT(*) = 1 AND MAX(Age) < 50) OR MIN(Rank) = 3 ORDER BY Species", "Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING SUM(Pay) > 12", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING AVG(Pay) > 4", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING MAX(Pay) > 5", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > 1 AND AVG(Pay) < 4.5", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING MIN(Pay) >= 2.5 AND MAX(Pay) <= 3.5 ORDER BY Species", "Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(DISTINCT Rank) > 1", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING MAX(Rank) - 1 > 3", "Human/3")]
        [DataRow("SELECT Species, SUM(Rank) FROM crew GROUP BY Species HAVING SUM(Rank) BETWEEN 3 AND 10 ORDER BY Species", "Moclan/3; Xelayan/3")]
        [DataRow("SELECT Species, MAX(Rank) FROM crew GROUP BY Species HAVING MAX(Rank) IN (2, 3) ORDER BY Species", "Kaylon/2; Moclan/3; Xelayan/3")]
        [DataRow("SELECT Species, MAX(Rank) FROM crew GROUP BY Species HAVING MAX(Rank) NOT IN (2, 3)", "Human/5")]
        [DataRow("SELECT Species, SUM(Age) FROM crew WHERE Rank < 5 GROUP BY Species HAVING SUM(Age) > 40 ORDER BY Species DESC", "Moclan/45; Kaylon/200; Human/74")]
        [DataRow("SELECT c.Species, MIN(c.Rank) FROM crew c WHERE c.ShipId IS NOT NULL GROUP BY c.Species HAVING MIN(c.Rank) >= 3 AND c.Species <> 'Xelayan'", "Moclan/3")]
        public void HavingFiltersGroupsOnTypedAggregates(string sql, string expected)
        {
            // Act
            var viaTables = Orville.Tables().Query<Tuple<string, int>>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(viaTables), "tables");
            Assert.AreEqual(expected, Orville.Rows(viaQuery), "query");
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING NOT COUNT(*) = 1", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING 1 < COUNT(*)", "Human/3")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING Species LIKE 'K%' OR COUNT(*) = 3 ORDER BY Species", "Human/3; Kaylon/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) >= 1 AND Species NOT IN ('Human', 'Kaylon') ORDER BY Species", "Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew WHERE ShipId IS NOT NULL GROUP BY Species HAVING COUNT(*) BETWEEN 1 AND 2 ORDER BY Species DESC", "Xelayan/1; Moclan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) IN (3, 4)", "Human/3")]
        public void HavingFiltersGroupsInTypedAndDictionaryRows(string sql, string expected)
        {
            // Act
            var typedTables = Orville.Tables().Query<Tuple<string, int>>(sql);
            var dictionaryTables = Orville.DictionaryTables().Query<Tuple<string, int>>(sql);
            var typed = Orville.Crew().Query<CrewMember, Tuple<string, int>>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typedTables), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaryTables), "dictionary tables");
            Assert.AreEqual(expected, Orville.Rows(typed), "typed");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionaries");
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) FROM crew HAVING AVG(Age) > 60", "6")]
        [DataRow("SELECT MAX(Age) FROM crew WHERE ShipId IS NOT NULL HAVING COUNT(*) = 5", "45")]
        [DataRow("SELECT COUNT(*) FROM crew HAVING MIN(Rank) > 2", "")]
        [DataRow("SELECT SUM(Rank) FROM crew WHERE Species = 'Human' HAVING MAX(Pay) > 5", "11")]
        [DataRow("SELECT COUNT(*) FROM crew c HAVING MAX(c.Rank) = 5 AND MIN(c.Rank) = 2", "6")]
        [DataRow("SELECT COUNT(*) FROM crew HAVING COUNT(*) > 5 OR MAX(Age) < 10", "6")]
        [DataRow("SELECT COUNT(*) FROM crew HAVING NOT COUNT(*) = 6", "")]
        [DataRow("SELECT COUNT(*) FROM crew WHERE Rank > 5", "0")]
        [DataRow("SELECT COUNT(*) FROM crew WHERE Rank > 5 HAVING COUNT(*) = 0", "0")]
        public void HavingFiltersAnUngroupedAggregate(string sql, string expected)
        {
            // Act
            var viaTables = Orville.Tables().Query<int>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, int>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", viaTables), "tables");
            Assert.AreEqual(expected, string.Join(", ", viaQuery), "query");
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) AS Total FROM crew GROUP BY Species HAVING Total > 1", "Human/3")]
        [DataRow("SELECT c.Species, MAX(c.Age) AS Total FROM crew c GROUP BY c.Species HAVING Total > 41 ORDER BY c.Species", "Kaylon/200; Moclan/45")]
        [DataRow("SELECT Species, SUM(Rank) AS Total FROM crew GROUP BY Species HAVING Total BETWEEN 3 AND 10 ORDER BY Species", "Moclan/3; Xelayan/3")]
        [DataRow("SELECT Species, COUNT(*) AS Total FROM crew GROUP BY Species HAVING Total = 1 OR Total = 3 ORDER BY Species", "Human/3; Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, MIN(Age) AS Total FROM crew GROUP BY Species HAVING Total < 35 ORDER BY Species", "Human/34; Xelayan/30")]
        [DataRow("SELECT Species, COUNT(*) AS Total FROM crew GROUP BY Species HAVING Total = 1 AND Species <> 'Kaylon' ORDER BY Species", "Moclan/1; Xelayan/1")]
        public void HavingFiltersTypedResultsByAlias(string sql, string expected)
        {
            // Act
            var viaQuery = Orville.Crew().Query<CrewMember, SpeciesTally>(sql);
            var viaTables = Orville.Tables().Query<SpeciesTally>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join("; ", viaQuery.Select(t => t.Species + "/" + t.Total)), "query");
            Assert.AreEqual(expected, string.Join("; ", viaTables.Select(t => t.Species + "/" + t.Total)), "tables");
        }

        [TestMethod]
        public void HavingFiltersJoinedTypedResultsByAlias()
        {
            // Act
            var result = Orville.Tables().Query<SpeciesTally>("SELECT Species, COUNT(*) AS Total FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY Species HAVING Total > 1");

            // Assert
            Assert.AreEqual("Human/3", string.Join("; ", result.Select(t => t.Species + "/" + t.Total)));
        }
    }
}
