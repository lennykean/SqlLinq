using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class AliasScopeTests
    {
        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Age BETWEEN 35 AND 45 ORDER BY c.Age", "Kelly Grayson/Human; Ed Mercer/Human; Bortus/Moclan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.ShipId IS NULL", "Isaac/Kaylon")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Name LIKE 'K%' OR c.Species = 'Moclan'", "Kelly Grayson/Human; Bortus/Moclan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank IN (2, 3) ORDER BY c.Rank DESC, c.Name", "Bortus/Moclan; Talla Keyali/Xelayan; Gordon Malloy/Human; Isaac/Kaylon")]
        [DataRow("SELECT Name, c.Species FROM crew c WHERE c.Rank = 3 AND Age < 40", "Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c ORDER BY c.Pay DESC", "Ed Mercer/Human; Kelly Grayson/Human; Talla Keyali/Xelayan; Bortus/Moclan; Gordon Malloy/Human; Isaac/Kaylon")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE NOT c.Species = 'Human' AND c.Age < 100 ORDER BY c.Name DESC", "Talla Keyali/Xelayan; Bortus/Moclan")]
        [DataRow("SELECT c.Name, c.Species AS Kind FROM crew c WHERE c.Rank * 10 = c.Age", "Kelly Grayson/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank < 4 ORDER BY c.Age DESC", "Isaac/Kaylon; Bortus/Moclan; Gordon Malloy/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Pay > 4", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE 3 = c.Rank", "Bortus/Moclan; Talla Keyali/Xelayan")]
        public void AliasesQualifyEveryClauseInEveryRowShape(string sql, string expected)
        {
            // Act
            var typedTables = Orville.Tables().Query<Tuple<string, string>>(sql);
            var dictionaryTables = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);
            var typed = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, Tuple<string, string>>(sql);
            var expandos = Orville.CrewExpandos().Query<dynamic, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typedTables), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaryTables), "dictionary tables");
            Assert.AreEqual(expected, Orville.Rows(typed), "typed");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionaries");
            Assert.AreEqual(expected, Orville.Rows(expandos), "expandos");
        }

        [TestMethod]
        [DataRow("SELECT this.Name, this.Species FROM this WHERE this.Age BETWEEN 35 AND 45 ORDER BY this.Age", "Kelly Grayson/Human; Ed Mercer/Human; Bortus/Moclan")]
        [DataRow("SELECT this.Name, this.Species FROM this WHERE this.Species IN ('Moclan', 'Kaylon') OR this.Name LIKE 'T%'", "Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT this.Name, this.Species FROM this WHERE this.ShipId IS NOT NULL AND this.Rank <= 2", "Gordon Malloy/Human")]
        [DataRow("SELECT this.Name, Species FROM this WHERE NOT this.Rank > 2 ORDER BY this.Name DESC", "Isaac/Kaylon; Gordon Malloy/Human")]
        public void ThisQualifiesEveryClauseInEveryRowShape(string sql, string expected)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = Orville.Crew() };

            // Act
            var typed = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, Tuple<string, string>>(sql);
            var expandos = Orville.CrewExpandos().Query<dynamic, Tuple<string, string>>(sql);
            var viaTables = tables.Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionaries");
            Assert.AreEqual(expected, Orville.Rows(expandos), "expandos");
            Assert.AreEqual(expected, Orville.Rows(viaTables), "tables");
        }

        [TestMethod]
        [DataRow("SELECT C.Name, c.SPECIES FROM crew c WHERE C.Rank = 5", "Ed Mercer/Human")]
        [DataRow("SELECT name, species FROM CREW WHERE RANK = 5 AND this.AGE > 40", "Ed Mercer/Human")]
        [DataRow("SELECT THIS.Name, this.species FROM this WHERE This.Rank = 5", "Ed Mercer/Human")]
        public void IdentifiersIgnoreCaseOnTypedAndExpandoRows(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);
            var expandos = Orville.CrewExpandos().Query<dynamic, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed");
            Assert.AreEqual(expected, Orville.Rows(expandos), "expandos");
        }

        [TestMethod]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING c.Species <> 'Human' AND COUNT(*) > 0 ORDER BY c.Species DESC", "Xelayan/1; Moclan/1; Kaylon/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) = 1 AND c.Species LIKE '%a%' ORDER BY c.Species", "Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c WHERE c.Rank < 5 GROUP BY c.Species HAVING COUNT(*) > 1", "Human/2")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c WHERE c.ShipId IS NOT NULL GROUP BY c.Species ORDER BY c.Species DESC", "Xelayan/1; Moclan/1; Human/3")]
        public void AliasesGroupTypedAndDictionaryRows(string sql, string expected)
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
        [DataRow("SELECT c.Species, SUM(c.Age) FROM crew c GROUP BY c.Species ORDER BY c.Species", "Human/115; Kaylon/200; Moclan/45; Xelayan/30")]
        [DataRow("SELECT c.Species, MAX(c.Rank) FROM crew c GROUP BY c.Species HAVING MAX(c.Rank) > 2 ORDER BY c.Species", "Human/5; Moclan/3; Xelayan/3")]
        [DataRow("SELECT c.Species, MIN(c.Age) FROM crew c WHERE c.ShipId IS NOT NULL GROUP BY c.Species ORDER BY c.Species DESC", "Xelayan/30; Moclan/45; Human/34")]
        [DataRow("SELECT c.Species, COUNT(ALL c.ShipId) FROM crew c GROUP BY c.Species ORDER BY c.Species", "Human/3; Kaylon/0; Moclan/1; Xelayan/1")]
        [DataRow("SELECT c.Species, COUNT(DISTINCT c.Rank) FROM crew c GROUP BY c.Species ORDER BY c.Species", "Human/3; Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT C.Species, MAX(c.AGE) FROM crew c GROUP BY c.SPECIES ORDER BY C.species", "Human/41; Kaylon/200; Moclan/45; Xelayan/30")]
        [DataRow("SELECT c.Species, SUM(c.Rank) FROM crew c WHERE c.Pay > 2 GROUP BY c.Species HAVING SUM(c.Rank) > 2 ORDER BY c.Species", "Human/11; Moclan/3; Xelayan/3")]
        public void AliasesGroupTypedFields(string sql, string expected)
        {
            // Act
            var viaTables = Orville.Tables().Query<Tuple<string, int>>(sql);
            var viaQuery = Orville.Crew().Query<CrewMember, Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(viaTables), "tables");
            Assert.AreEqual(expected, Orville.Rows(viaQuery), "query");
        }

        [TestMethod]
        [DataRow("SELECT this.Species, SUM(this.Rank) FROM this GROUP BY this.Species HAVING SUM(this.Rank) > 2 ORDER BY this.Species", "Human/11; Moclan/3; Xelayan/3")]
        [DataRow("SELECT this.Species, COUNT(*) FROM this WHERE this.Rank < 5 GROUP BY this.Species HAVING COUNT(*) > 1", "Human/2")]
        [DataRow("SELECT this.Species, MAX(this.Age) FROM this GROUP BY this.Species ORDER BY this.Species DESC", "Xelayan/30; Moclan/45; Kaylon/200; Human/41")]
        public void ThisQualifiesGroupedQueries(string sql, string expected)
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable> { ["this"] = Orville.Crew() };

            // Act
            var viaQuery = Orville.Crew().Query<CrewMember, Tuple<string, int>>(sql);
            var viaTables = tables.Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(viaQuery), "query");
            Assert.AreEqual(expected, Orville.Rows(viaTables), "tables");
        }

        [TestMethod]
        [DataRow("SELECT this.Name, this.Species FROM this WHERE this.Rank > (SELECT AVG(this.Rank) FROM this)", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, c.Species FROM this c WHERE c.Age = (SELECT MAX(c2.Age) FROM this c2 WHERE c2.ShipId IS NOT NULL)", "Bortus/Moclan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Pay = (SELECT MIN(x.Pay) FROM crew x)", "Isaac/Kaylon")]
        public void AliasesQualifySubqueriesOverTheQuerySource(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }
    }
}
