using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class JoinCompatibilityTests
    {
        private const string AllPostings = "Ed Mercer/Orville; Kelly Grayson/Orville; Bortus/Orville; Gordon Malloy/Orville; Talla Keyali/Roosevelt";

        [TestMethod]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON that.Id = this.ShipId", AllPostings)]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id", AllPostings)]
        [DataRow("SELECT this.Name, Class FROM this INNER JOIN that ON this.ShipId = that.Id WHERE Species = 'Xelayan'", "Talla Keyali/Exploratory")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id WHERE that.Name = 'Orville' AND this.Rank >= 4", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id WHERE this.Name LIKE '%a%'", "Kelly Grayson/Orville; Gordon Malloy/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id ORDER BY that.Name DESC, this.Name", "Talla Keyali/Roosevelt; Bortus/Orville; Ed Mercer/Orville; Gordon Malloy/Orville; Kelly Grayson/Orville")]
        public void JoinsSelectTheExpectedPostings(string sql, string expected)
        {
            // Act
            var typed = Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>(sql, Orville.Ships());
            var dictionaries = Orville.CrewDictionaries().Query<IDictionary<string, object>, Ship, Tuple<string, string>>(sql, Orville.Ships());

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed));
            Assert.AreEqual(expected, Orville.Rows(dictionaries));
        }

        [TestMethod]
        [DataRow("SELECT that.Name, COUNT(*) FROM this INNER JOIN that ON this.ShipId = that.Id GROUP BY that.Name ORDER BY that.Name", "Orville/4; Roosevelt/1")]
        [DataRow("SELECT that.Class, COUNT(*) FROM this INNER JOIN that ON this.ShipId = that.Id GROUP BY that.Class", "Exploratory/5")]
        public void JoinsGroupTheExpectedTotals(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Ship, Tuple<string, int>>(sql, Orville.Ships());

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void EveryMatchingInnerRowProducesARow()
        {
            // Arrange
            var ships = Orville.Ships();
            ships.Add(new Ship { Id = 1, Name = "Orville Shuttle", Class = "Shuttle" });

            // Act
            var result = Orville.Crew().Query<CrewMember, Ship, Tuple<string, string>>("SELECT this.Name, that.Name AS Ship FROM this INNER JOIN that ON this.ShipId = that.Id WHERE this.Name = 'Bortus'", ships);

            // Assert
            Assert.AreEqual("Bortus/Orville; Bortus/Orville Shuttle", Orville.Rows(result));
        }
    }
}
