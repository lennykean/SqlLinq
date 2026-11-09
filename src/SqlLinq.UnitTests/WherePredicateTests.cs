using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class WherePredicateTests
    {
        [TestMethod]
        [DataRow("SELECT Name FROM crew WHERE Rank = 3", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank <> 3", "Ed Mercer, Kelly Grayson, Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank != 3", "Ed Mercer, Kelly Grayson, Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank >= 4", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT Name FROM crew WHERE Rank <= 2", "Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank < 3", "Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank > 4", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE 4 < Rank", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE 3 = Rank", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Age >= 200", "Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Age BETWEEN 40 AND 45", "Ed Mercer, Kelly Grayson, Bortus")]
        [DataRow("SELECT Name FROM crew WHERE Age NOT BETWEEN 40 AND 45", "Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Species IN ('Moclan', 'Xelayan')", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Species NOT IN ('Human', 'Kaylon')", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank IN (5, 2)", "Ed Mercer, Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank NOT IN (5, 2)", "Kelly Grayson, Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Name LIKE '%a%'", "Kelly Grayson, Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Name NOT LIKE '%a%'", "Ed Mercer, Bortus")]
        [DataRow("SELECT Name FROM crew WHERE Name LIKE '_d%'", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE Name LIKE '%s'", "Bortus")]
        [DataRow("SELECT Name FROM crew WHERE Name LIKE 'Isaac'", "Isaac")]
        [DataRow("SELECT Name FROM crew WHERE ShipId IS NULL", "Isaac")]
        [DataRow("SELECT Name FROM crew WHERE ShipId IS NOT NULL", "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE NOT (Rank = 3 OR Species = 'Human')", "Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Species = 'Human' OR Species = 'Moclan' AND Rank > 3", "Ed Mercer, Kelly Grayson, Gordon Malloy")]
        [DataRow("SELECT Name FROM crew WHERE (Species = 'Human' OR Species = 'Moclan') AND Rank > 3", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT Name FROM crew WHERE NOT Species = 'Human' AND Rank = 3", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank = 2 OR Rank = 3 AND Age > 40", "Bortus, Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Age = 40 AND Rank = 4 OR Name = 'Isaac'", "Kelly Grayson, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank > 1 AND Rank < 5 AND Species <> 'Human' AND ShipId IS NOT NULL", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank * 10 = Age", "Kelly Grayson, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Age - 50 < -10", "Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank + 1 = 6", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE (Rank + 1) * 2 = 8", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE -Rank = -5", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE Rank = 3.0", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Rank < 2.5", "Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Rank >= 2.5", "Ed Mercer, Kelly Grayson, Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Pay > 4", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT Name FROM crew WHERE Pay > 4.5", "Ed Mercer")]
        [DataRow("SELECT Name FROM crew WHERE Pay >= 3.5", "Ed Mercer, Kelly Grayson, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Pay = 3", "Bortus")]
        [DataRow("SELECT Name FROM crew WHERE Pay < 3", "Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Pay < 2.5", "Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Pay BETWEEN 2.5 AND 3.5", "Bortus, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Pay IN (2.5, 3.5)", "Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT Name FROM crew WHERE Pay NOT IN (5.5, 4.5, 3.5)", "Bortus, Gordon Malloy, Isaac")]
        [DataRow("SELECT Name FROM crew WHERE Pay * 2 = 9", "Kelly Grayson")]
        [DataRow("SELECT Name FROM crew WHERE Pay - Rank = 0.5", "Ed Mercer, Kelly Grayson, Gordon Malloy, Talla Keyali")]
        public void PredicatesSelectTheSameCrewFromEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, string>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, string>(sql);
            var expandos = Orville.CrewExpandos().Query<dynamic, string>(sql);
            var typedTables = Orville.Tables().Query<string>(sql);
            var dictionaryTables = Orville.DictionaryTables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed), "typed");
            Assert.AreEqual(expected, string.Join(", ", dictionaries), "dictionaries");
            Assert.AreEqual(expected, string.Join(", ", expandos), "expandos");
            Assert.AreEqual(expected, string.Join(", ", typedTables), "typed tables");
            Assert.AreEqual(expected, string.Join(", ", dictionaryTables), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT Name FROM planets WHERE Gravity > 1", "Xelaya, Kaylon 1")]
        [DataRow("SELECT Name FROM planets WHERE Gravity = 1", "Earth")]
        [DataRow("SELECT Name FROM planets WHERE Gravity >= 1.25", "Xelaya, Kaylon 1")]
        [DataRow("SELECT Name FROM planets WHERE Gravity < 1", "Moclus")]
        [DataRow("SELECT Name FROM planets WHERE Gravity > 1.1", "Xelaya, Kaylon 1")]
        [DataRow("SELECT Name FROM planets WHERE Gravity < 1.1", "Earth, Moclus")]
        [DataRow("SELECT Name FROM planets WHERE Gravity * 2 = 7", "Xelaya")]
        [DataRow("SELECT Name FROM planets WHERE Gravity / 2 = 0.5", "Earth")]
        [DataRow("SELECT Name FROM planets WHERE Gravity BETWEEN 0.75 AND 1.25", "Earth, Moclus, Kaylon 1")]
        [DataRow("SELECT Name FROM planets WHERE Gravity IN (0.75, 3.5)", "Moclus, Xelaya")]
        [DataRow("SELECT Name FROM planets WHERE Gravity + Moons > 3", "Xelaya, Kaylon 1")]
        [DataRow("SELECT Name FROM planets WHERE Gravity - Moons < 0", "Kaylon 1")]
        [DataRow("SELECT Name FROM planets WHERE Moons < 1.5", "Earth, Moclus")]
        [DataRow("SELECT Name FROM planets WHERE Name LIKE '% 1'", "Kaylon 1")]
        public void PredicatesSelectTheSamePlanetsFromEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Planets().Query<Planet, string>(sql);
            var dictionaries = Orville.PlanetDictionaries().Query<IDictionary<string, object>, string>(sql);
            var typedTables = Orville.Tables().Query<string>(sql);
            var dictionaryTables = Orville.DictionaryTables().Query<string>(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed), "typed");
            Assert.AreEqual(expected, string.Join(", ", dictionaries), "dictionaries");
            Assert.AreEqual(expected, string.Join(", ", typedTables), "typed tables");
            Assert.AreEqual(expected, string.Join(", ", dictionaryTables), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Pay > Rank", "Ed Mercer, Kelly Grayson, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Pay >= Rank", "Ed Mercer, Kelly Grayson, Bortus, Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT * FROM this WHERE Age - Rank > 36", "Bortus, Isaac")]
        [DataRow("SELECT * FROM this WHERE Age < Rank * 10", "Ed Mercer")]
        [DataRow("SELECT * FROM this WHERE Pay * 10 > Age", "Ed Mercer, Kelly Grayson, Talla Keyali")]
        public void TypedFieldsCompareWithEachOther(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result.Select(c => c.Name)));
        }
    }
}
