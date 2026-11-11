using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class SqlSyntaxTests
    {
        [TestMethod]
        [DataRow("Rank", "Rank")]
        [DataRow("[Rank]", "Rank")]
        [DataRow("[Ship Id]", "Ship Id")]
        [DataRow("[Ship.Id]", "Ship.Id")]
        [DataRow("c.Rank", "c.Rank")]
        [DataRow("[c].Rank", "[c].Rank")]
        [DataRow("c.[Rank]", "c.[Rank]")]
        [DataRow("[a]]b]", "a]]b")]
        [DataRow("[]", "")]
        [DataRow("[", "[")]
        [DataRow("'Rank'", "'Rank'")]
        public void UnbracketTakesTheBracketsOffABracketedName(string text, string expected)
        {
            // Act
            var result = SqlSyntax.Unbracket(text);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("[c].[Rank]", "c.Rank")]
        [DataRow("[Drive].[Spin axis]", "Drive.Spin axis")]
        [DataRow("[Ship.Id].[Name]", "Ship.Id.Name")]
        public void UnbracketTakesTheBracketsOffBothPartsOfADottedName(string text, string expected)
        {
            // Act
            var result = SqlSyntax.Unbracket(text);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TokensHaveTheirKindAndPosition()
        {
            // Arrange
            const string Sql = "SELECT abs(Rank) AS r, [Ship Id], c.[Pay], COUNT(*) FROM crew c -- crew\nWHERE Name = 'Ed' AND Pay >= 2.5 OR Born = #2410-01-01# AND ShipId IS NOT NULL /* all */";
            var expected = new[]
            {
                "Keyword SELECT", "FunctionName abs", "Operator (", "Identifier Rank", "Operator )", "Keyword AS", "Identifier r", "Operator ,",
                "BracketedIdentifier [Ship Id]", "Operator ,", "BracketedIdentifier c.[Pay]", "Operator ,", "FunctionName COUNT", "Operator (", "Operator *", "Operator )",
                "Keyword FROM", "Identifier crew", "Identifier c", "Comment -- crew",
                "Keyword WHERE", "Identifier Name", "Operator =", "StringLiteral 'Ed'", "Keyword AND", "Identifier Pay", "Operator >=", "NumberLiteral 2.5",
                "Keyword OR", "Identifier Born", "Operator =", "StringLiteral #2410-01-01#", "Keyword AND", "Identifier ShipId", "Keyword IS", "Keyword NOT", "Keyword NULL",
                "Comment /* all */",
            };

            // Act
            var tokens = SqlSyntax.Tokenize(Sql);

            // Assert
            CollectionAssert.AreEqual(expected, tokens.Select(t => t.Kind + " " + t.Text).ToArray());
            Assert.IsTrue(tokens.All(t => Sql.Substring(t.Start, t.Text.Length) == t.Text));
            Assert.IsTrue(tokens.Zip(tokens.Skip(1), (a, b) => a.Start + a.Text.Length <= b.Start).All(ordered => ordered));
            Assert.AreEqual(Sql.IndexOf("-- crew", StringComparison.Ordinal), tokens.Single(t => t.Text == "-- crew").Start);
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Name = 'Ed", "StringLiteral 'Ed")]
        [DataRow("SELECT * FROM this /* open\nstill open", "Comment /* open\nstill open")]
        [DataRow("SELECT * FROM this /* a /* b */ c */", "Comment /* a /* b */ c */")]
        [DataRow("SELECT * FROM this -- note", "Comment -- note")]
        [DataRow("SELECT * FROM this WHERE Rank > @x", "Identifier x")]
        [DataRow("SELECT * FROM this WHERE", "Keyword WHERE")]
        [DataRow("SELECT * FROM this WHERE Rank */", "Unknown */")]
        public void IncompleteTextStillTokenizes(string sql, string last)
        {
            // Act
            var tokens = SqlSyntax.Tokenize(sql);

            // Assert
            Assert.AreEqual(last, tokens[tokens.Count - 1].Kind + " " + tokens[tokens.Count - 1].Text);
        }

        [TestMethod]
        public void UnterminatedStringEndsAtItsLine()
        {
            // Act
            var tokens = SqlSyntax.Tokenize("WHERE Name = 'Ed\nAND Rank > 1");

            // Assert
            CollectionAssert.AreEqual(new object[] { "StringLiteral 'Ed", "Keyword AND", "Identifier Rank" }, tokens.Skip(3).Take(3).Select(t => t.Kind + " " + t.Text).ToArray());
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   \r\n\t")]
        public void BlankTextHasNoTokens(string text)
        {
            // Act
            var tokens = SqlSyntax.Tokenize(text);

            // Assert
            Assert.IsEmpty(tokens);
        }

        [TestMethod]
        public void UnknownCharactersAreTokensOfTheirOwn()
        {
            // Act
            var tokens = SqlSyntax.Tokenize("Rank > @x;");

            // Assert
            CollectionAssert.AreEqual(new object[] { "Identifier Rank", "Operator >", "Unknown @", "Identifier x", "Unknown ;" }, tokens.Select(t => t.Kind + " " + t.Text).ToArray());
        }

        [TestMethod]
        public void ListedWordsTokenizeAsTheirKind()
        {
            // Act
            var keywords = SqlSyntax.Keywords.Select(k => SqlSyntax.Tokenize(k).Single().Kind).Distinct().ToList();
            var aggregates = SqlSyntax.Aggregates.Select(a => SqlSyntax.Tokenize(a + "(x)")[0].Kind).Distinct().ToList();
            var functions = SqlSyntax.Functions.Select(f => SqlSyntax.Tokenize(f + "(x)")[0].Kind).Distinct().ToList();

            // Assert
            CollectionAssert.AreEqual(new object[] { SqlTokenKind.Keyword }, keywords);
            CollectionAssert.AreEqual(new object[] { SqlTokenKind.FunctionName }, aggregates);
            CollectionAssert.AreEqual(new object[] { SqlTokenKind.FunctionName }, functions);
            Assert.Contains("abs", SqlSyntax.Functions);
            Assert.Contains("SELECT", SqlSyntax.Keywords);
        }
    }
}
