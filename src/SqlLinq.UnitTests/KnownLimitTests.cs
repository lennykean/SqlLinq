using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class KnownLimitTests
    {
        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) AS Total FROM crew GROUP BY Species HAVING Total > 1 AND COUNT(*) > 1", "Human/3")]
        [DataRow("SELECT Species, MAX(Age) AS Total FROM crew GROUP BY Species HAVING Total > 41 OR MIN(Rank) = 4 ORDER BY Species", "Kaylon/200; Moclan/45")]
        public void HavingMixesAResultAliasWithAnAggregate(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, SpeciesTally>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join("; ", result.Select(t => t.Species + "/" + t.Total)));
        }

        [TestMethod]
        [DataRow("SELECT Species, MAX(Rank) FROM crew GROUP BY Species HAVING MAX(Rank) = (SELECT MAX(Rank) FROM crew)", "Human/5")]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) = (SELECT COUNT(*) FROM crew) / 2", "Human/3")]
        public void HavingSubqueriesOverTheOuterTableReadTheWholeTable(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT s.Class FROM classes k WHERE k.Role = 'Research') = 'Science'", "Pembroke/Science")]
        public void OuterReferencesBindInASubquerySelectList(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT a.Name, b.Name AS Junior FROM crew a JOIN crew b ON a.Species = b.Species WHERE a.Rank > b.Rank AND a.Rank = 5", "Ed Mercer/Kelly Grayson; Ed Mercer/Gordon Malloy")]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay > Rank AND Species <> 'Human'", "Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id AND c.Rank < s.Id + 2", "Gordon Malloy/Orville; Talla Keyali/Roosevelt")]
        public void UntypedValuesCompareWithEachOther(string sql, string expected)
        {
            // Act
            var result = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank BETWEEN (SELECT Rank FROM crew WHERE Species = 'Human') AND 5")]
        [DataRow("SELECT Name FROM crew WHERE (SELECT Rank FROM crew WHERE Species = 'Human') IN (2, 3)")]
        public void MoreThanOneRowInsideBetweenOrInThrows(string sql)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Tables().Query(sql).ToList());

            // Assert
            Assert.Contains("The subquery returned more than one row.", e.Message);
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 3", "Bortus/Orville; Talla Keyali/Roosevelt")]
        public void SameNamedColumnsFromTwoTablesStaySeparate(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Species, SUM(Age) FROM crew GROUP BY Species ORDER BY Species", "Human/115; Kaylon/200; Moclan/45; Xelayan/30")]
        [DataRow("SELECT Species, MAX(Rank) FROM crew GROUP BY Species HAVING AVG(Pay) > 4", "Human/5")]
        [DataRow("SELECT s.Name, MAX(c.Rank) FROM crew c JOIN ships s ON c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/5; Roosevelt/3")]
        [DataRow("SELECT s.Name, SUM(c.Age) FROM crew c, ships s WHERE c.ShipId = s.Id GROUP BY s.Name ORDER BY s.Name", "Orville/160; Roosevelt/30")]
        public void AggregatesReadDictionaryAndJoinedFields(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, int>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank IN (2, (SELECT MAX(Rank) FROM crew))", "Ed Mercer, Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Species IN ((SELECT Species FROM planets WHERE Moons = 0), 'Kaylon')", "Bortus, Isaac")]
        public void InListsIncludeSubqueryValues(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }
    }
}
