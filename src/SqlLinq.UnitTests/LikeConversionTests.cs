using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SqlLinq.SyntaxTree.Expressions.Predicates.Comparison;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class LikeConversionTests
    {
        [TestMethod]
        public void RegexEndsWithConversion()
        {
            string regex = LikeNode.ConvertLikeToRegex("%def");

            Assert.IsTrue(LikeNode.IsRegexMatch("abcdef", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("abcdef123", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("def123", regex));
        }

        [TestMethod]
        public void RegexBeginsWithConversion()
        {
            string regex = LikeNode.ConvertLikeToRegex("abc%");

            Assert.IsTrue(LikeNode.IsRegexMatch("abcdef", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("123abcdef", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("123abc", regex));
        }

        [TestMethod]
        public void RegexNoWildcard()
        {
            string regex = LikeNode.ConvertLikeToRegex("bcd");

            Assert.IsFalse(LikeNode.IsRegexMatch("abcdef", regex));
        }

        [TestMethod]
        public void RegexContains()
        {
            string regex = LikeNode.ConvertLikeToRegex("%bcd%");

            Assert.IsTrue(LikeNode.IsRegexMatch("abcdef", regex));
            Assert.IsTrue(LikeNode.IsRegexMatch("bcdef", regex));
            Assert.IsTrue(LikeNode.IsRegexMatch("abcd", regex));
            Assert.IsTrue(LikeNode.IsRegexMatch("bcd", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("def", regex));
        }

        [TestMethod]
        public void RegexPercentLiteral()
        {
            string regex = LikeNode.ConvertLikeToRegex("e[%]");

            Assert.IsTrue(LikeNode.IsRegexMatch("e%", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("e", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("%", regex));
        }

        [TestMethod]
        public void RegexPercentLiteral2()
        {
            string regex = LikeNode.ConvertLikeToRegex("%e[%]");

            Assert.IsTrue(LikeNode.IsRegexMatch("be%", regex));
            Assert.IsTrue(LikeNode.IsRegexMatch("e%", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("e%d", regex));
        }

        [TestMethod]
        public void RegexSingleCharacterWildcard()
        {
            string regex = LikeNode.ConvertLikeToRegex("b_n");

            Assert.IsTrue(LikeNode.IsRegexMatch("bin", regex));
            Assert.IsTrue(LikeNode.IsRegexMatch("bun", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("bid", regex));
        }

        [TestMethod]
        public void RegexSingleCharacterWildcardLiteral()
        {
            string regex = LikeNode.ConvertLikeToRegex("b[_]n");

            Assert.IsTrue(LikeNode.IsRegexMatch("b_n", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("bin", regex));
        }

        [TestMethod]
        public void RegexNegatedCharacterSet()
        {
            // Act
            var regex = LikeNode.ConvertLikeToRegex("Mocl[^u]n");

            // Assert
            Assert.IsTrue(LikeNode.IsRegexMatch("Moclan", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("Moclun", regex));
        }

        [TestMethod]
        public void RegexMetacharactersAreLiteral()
        {
            // Act
            var regex = LikeNode.ConvertLikeToRegex("Cmdr. Grayson+");

            // Assert
            Assert.IsTrue(LikeNode.IsRegexMatch("Cmdr. Grayson+", regex));
            Assert.IsFalse(LikeNode.IsRegexMatch("Cmdrs Graysonn", regex));
        }

        [TestMethod]
        [DataRow("%a%b%", "", "xaxbx", true)]
        [DataRow("%a%b", "", "ab", true)]
        [DataRow("%a%b", "", "ba", false)]
        [DataRow("%ab%b", "", "ab", false)]
        [DataRow("%ab%b", "", "abab", true)]
        [DataRow("%aa%ab", "", "aaab", true)]
        [DataRow("%ab%ba", "", "aba", false)]
        [DataRow("%a%a", "", "aa", true)]
        [DataRow("%a%a", "", "a", false)]
        [DataRow("a%b%c", "", "abc", true)]
        [DataRow("a%b%c", "", "acb", false)]
        [DataRow("%a_%b", "", "xayzb", true)]
        [DataRow("%[xy]_%b", "", "qyab", true)]
        [DataRow("%[^a]%[^a]", "", "aab", false)]
        [DataRow("%[^a]%[^a]", "", "abab", true)]
        [DataRow("%%a%%", "", "a", true)]
        [DataRow("%e[%]%", "", "be%d", true)]
        [DataRow("%a%a%a%b", "", "aaab", true)]
        [DataRow("%a%a%a%b", "", "aab", false)]
        [DataRow("%A%B", "", "xaxb", true)]
        [DataRow("%a!%%b", "!", "xa%yb", true)]
        [DataRow("%a!%%b", "!", "xayb", false)]
        [DataRow("a%b", "", "a\nb", true)]
        [DataRow("a_b", "", "a\nb", true)]
        [DataRow("%[^a]%", "", "Ab\nbaAa", true)]
        public void ManyWildcardsMatchLikeSql(string like, string escape, string input, bool expected)
        {
            // Arrange
            var regex = escape.Length == 0 ? LikeNode.ConvertLikeToRegex(like) : LikeNode.ConvertLikeToRegex(like, escape[0]);

            // Act
            var matched = LikeNode.IsRegexMatch(input, regex);

            // Assert
            Assert.AreEqual(expected, matched);
        }

        [TestMethod]
        [Timeout(10000)]
        public void ManyWildcardsDoNotBacktrack()
        {
            // Arrange
            var ships = new List<Ship> { new Ship { Name = new string('a', 100000) } };

            // Act
            var matched = ships.Query("SELECT * FROM this WHERE Name LIKE '%a%a%a%a%a%b'").ToList();

            // Assert
            Assert.IsEmpty(matched);
        }
    }
}
