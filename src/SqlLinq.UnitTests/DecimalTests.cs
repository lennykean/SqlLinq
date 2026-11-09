using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class DecimalTests
    {
        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Pay > 4", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT * FROM this WHERE Pay >= 5", "Ed Mercer")]
        [DataRow("SELECT * FROM this WHERE Pay < 3", "Gordon Malloy, Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay <= 2", "Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay = 3", "Bortus")]
        [DataRow("SELECT * FROM this WHERE Pay <> 3 AND Pay < 4", "Gordon Malloy, Talla Keyali, Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay BETWEEN 3 AND 4", "Bortus, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Pay NOT BETWEEN 3 AND 4", "Ed Mercer, Kelly Grayson, Gordon Malloy, Isaac")]
        [DataRow("SELECT * FROM this WHERE Pay > Rank", "Ed Mercer, Kelly Grayson, Gordon Malloy, Talla Keyali")]
        [DataRow("SELECT * FROM this WHERE Pay > 4.25", "Ed Mercer, Kelly Grayson")]
        public void DecimalsCompareWithoutLosingTheFraction(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query(sql);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result.Select(c => c.Name)));
        }
    }
}
