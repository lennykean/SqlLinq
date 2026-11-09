using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class SingleSourceCompatibilityTests
    {
        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank > 3", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM crew WHERE Rank > 3", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM this WHERE Age / 2 = 20", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM this WHERE Pay > 4", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM this WHERE Pay BETWEEN 3 AND 4", "Bortus, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE ShipId IS NULL", "Isaac")]
        [DataRow("SELECT * FROM this WHERE ShipId = 2", "Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Species IN ('Moclan', 'Kaylon')", "Bortus, Isaac")]
        [DataRow("SELECT * FROM this WHERE NOT Species = 'Human'", "Bortus, Talla Keyali, Isaac")]
        [DataRow("SELECT * FROM this WHERE Species = 'Human' AND Rank < 5", "Kelly Grayson, Gordon Malloy")]
        [DataRow("SELECT * FROM this WHERE Name LIKE '%a%' OR Rank = 5", "Ed Mercer, Kelly Grayson, Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT * FROM this WHERE Rank > (SELECT AVG(Rank) FROM this)", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM this ORDER BY Rank DESC, Name", "Ed Mercer, Kelly Grayson, Bortus, Talla Keyali, Gordon Malloy, Isaac")]
        public void TypedQueriesSelectTheExpectedCrew(string sql, string expected)
        {
            // Arrange
            var crew = Orville.Crew();

            // Act
            var result = crew.Query(sql).ToList();

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result.Select(c => c.Name)));
            Assert.IsTrue(result.All(r => crew.Contains(r)), "SELECT * returns the original objects");
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM this WHERE Rank > 3", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM this WHERE ShipId IS NULL", "Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM this WHERE Species IN ('Moclan', 'Kaylon') ORDER BY Name", "Bortus/Moclan; Isaac/Kaylon")]
        public void TypedAndDictionaryRowsSelectTheSameTuples(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM this GROUP BY Species ORDER BY Species", "Human/3; Kaylon/1; Moclan/1; Xelayan/1")]
        [DataRow("SELECT Species, COUNT(*) FROM this GROUP BY Species ORDER BY Species DESC", "Xelayan/1; Moclan/1; Kaylon/1; Human/3")]
        [DataRow("SELECT Species, MAX(Rank) FROM this GROUP BY Species ORDER BY Species", "Human/5; Kaylon/2; Moclan/3; Xelayan/3")]
        [DataRow("SELECT Species, SUM(Age) FROM this WHERE Rank > 2 GROUP BY Species ORDER BY Species", "Human/81; Moclan/45; Xelayan/30")]
        public void GroupedQueriesSelectTheExpectedTotals(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE")]
        [DataRow("SELECT FROM this")]
        [DataRow("SELECT * FROM this WHERE Rank >> 3")]
        [DataRow("SELECT * FROM this ORDER Name")]
        public void ParseErrorsThrowSqlException(string sql)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Orville.Crew().Query(sql));
            Assert.StartsWith("SQL parse error", e.Message);
        }

        [TestMethod]
        public void TypedRowsSelectIntoEveryResultShape()
        {
            // Arrange
            const string Sql = "SELECT Name, Species AS Kind FROM this WHERE Name = 'Bortus'";
            var crew = Orville.Crew();

            // Act
            var dictionary = crew.Query<CrewMember, IDictionary<string, object>>(Sql).Single();
            var expando = crew.Query<CrewMember, dynamic>(Sql).Single();
            var tuple = crew.Query<CrewMember, Tuple<string, string>>(Sql).Single();
            var single = crew.Query<CrewMember, string>("SELECT Name FROM this WHERE Rank = 5").Single();

            // Assert
            Assert.AreEqual("Moclan", dictionary["Kind"]);
            Assert.AreEqual("Moclan", expando.Kind);
            Assert.AreEqual(Tuple.Create("Bortus", "Moclan"), tuple);
            Assert.AreEqual("Ed Mercer", single);
        }

        [TestMethod]
        public void ExpandoObjectsQueriedAsDynamic()
        {
            // Arrange
            var expandos = new List<ExpandoObject>();
            foreach (var member in Orville.Crew())
            {
                dynamic expando = new ExpandoObject();
                expando.Name = member.Name;
                expando.Rank = member.Rank;
                expandos.Add(expando);
            }

            IEnumerable<dynamic> crew = expandos;

            // Act
            var result = crew.Query<dynamic, dynamic>("SELECT Name, Rank FROM this WHERE Rank > 3").ToList();

            // Assert
            Assert.HasCount(2, result);
            Assert.AreEqual("Ed Mercer", result[0].Name);
            Assert.AreEqual(5, result[0].Rank);
        }

        [TestMethod]
        public void EmptySourceReturnsNoRows()
        {
            // Act
            var result = new List<CrewMember>().Query("SELECT * FROM this WHERE Rank > 3");

            // Assert
            Assert.IsEmpty(result.ToList());
        }
    }
}
