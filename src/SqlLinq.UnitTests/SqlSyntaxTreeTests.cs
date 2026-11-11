using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class SqlSyntaxTreeTests
    {
        private static readonly ISqlFunction[] Functions =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
        ];

        [TestMethod]
        public void ParseOfEveryPrefixOfAStatementNeverThrows()
        {
            // Arrange
            const string Sql = "SELECT c.Name, COUNT(*) AS N FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Name LIKE 'K%' /* note */ AND c.Rank IN (SELECT Rank FROM crew) GROUP BY c.Name HAVING COUNT(*) > 1 ORDER BY N DESC -- done";

            // Act
            var parsed = Enumerable.Range(0, Sql.Length + 1).Select(n => SqlSyntaxTree.Parse(Sql.Substring(0, n))).ToList();

            // Assert
            Assert.IsNull(parsed[^1].Error);
            Assert.IsTrue(parsed.Select((syntax, length) => syntax.Root != null || syntax.Error != null && syntax.Error.Start <= length).All(read => read));
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank >", "")]
        [DataRow("SELECT * FROM this /* unclosed", "")]
        [DataRow("SELECT * FROM this WHERE Name = 'Ed", "'")]
        [DataRow("SELECT Name, FROM this", "FROM")]
        [DataRow("SELECT * FROM this WHERE Age 18", "18")]
        [DataRow("SELECT Name FROM this WHERE twice(Rank > 6", "twice(Rank > 6")]
        public void ParseErrorsCarryTheirPosition(string sql, string text)
        {
            // Act
            var error = SqlSyntaxTree.Parse(sql, Functions).Error;

            // Assert
            Assert.AreEqual(sql.LastIndexOf(text, System.StringComparison.Ordinal), error.Start);
            Assert.AreEqual(text.Length, error.Length);
        }

        [TestMethod]
        public void RootIsTheStatementOrTheExpression()
        {
            // Act
            var select = SqlSyntaxTree.Parse("SELECT Name FROM this").Root;
            var union = SqlSyntaxTree.Parse("SELECT Name FROM crew UNION SELECT Name FROM ships").Root;
            var expression = SqlSyntaxTree.ParseExpression("Rank + 1").Root;
            var broken = SqlSyntaxTree.Parse("SELECT Name FROM").Root;

            // Assert
            Assert.IsInstanceOfType<SelectNode>(select);
            Assert.IsNotInstanceOfType<SelectNode>(union);
            Assert.IsInstanceOfType<ExpressionNode>(expression);
            Assert.IsNull(expression.Parent);
            Assert.IsNull(broken);
        }

        [TestMethod]
        [DataRow("SELECT Name, Rank * 2 AS R FROM this WHERE Age > 40 ORDER BY Name", "Name@7, Rank@13, Age@43, Name@61")]
        [DataRow("SELECT Species, COUNT(*) AS N FROM this GROUP BY Species HAVING N > 1", "Species@7, Species@49")]
        [DataRow("SELECT Name, Rank FROM this WHERE", "Name@7, Rank@13")]
        [DataRow("SELECT Name AS Who FROM this WHERE Age >= 18 ORDER BY Who", "Name@7, Age@35")]
        [DataRow("SELECT twice(Rank) FROM this WHERE twice(Age + Pay) > 1", "Rank@13, Age@41, Pay@47")]
        public void NamesAreTheMembersTheTextReads(string sql, string expected)
        {
            // Act
            var names = SqlSyntaxTree.Parse(sql, Functions).Names;

            // Assert
            Assert.AreEqual(expected, string.Join(", ", names.Select(n => n.LookupId + "@" + n.Start)));
            Assert.IsTrue(names.All(n => sql.Substring(n.Start, n.Length) == n.LookupId));
        }

        [TestMethod]
        [DataRow("select  Name,Rank*2 as R from this where abs(Rank)>1", "select Name, Rank * 2 as R from this where abs(Rank) > 1")]
        [DataRow("SELECT Name -- the name\nFROM this /* all */ WHERE NOT (Rank IN (1, 2))", "SELECT Name FROM this WHERE NOT (Rank IN (1, 2))")]
        [DataRow("SELECT twice( Rank ) FROM this", "SELECT twice(Rank) FROM this")]
        public void ToSqlWritesTheTreeBack(string sql, string expected)
        {
            // Act
            var written = SqlSyntaxTree.Parse(sql, Functions).Root.ToSql();

            // Assert
            Assert.AreEqual(expected, written);
            Assert.IsNull(SqlSyntaxTree.Parse(written, Functions).Error);
        }

        [TestMethod]
        public void ReplaceLeavesTheTreeItCopiesAlone()
        {
            // Arrange
            var syntax = SqlSyntaxTree.Parse("SELECT Name FROM this WHERE Heavy");
            var heavy = SqlSyntaxTree.ParseExpression("Weight > 150").Root;

            // Act
            var rewritten = syntax.Replace(n => n is NodeWithId id && id.Id.LookupId == "Heavy" ? heavy : null);

            // Assert
            Assert.AreEqual("SELECT Name FROM this WHERE Heavy", syntax.Root.ToSql());
            Assert.AreEqual("SELECT Name FROM this WHERE Weight > 150", rewritten.Root.ToSql());
            Assert.AreEqual(syntax.Text, rewritten.Text);
            Assert.AreEqual("Name, Weight", string.Join(", ", rewritten.Names.Select(n => n.LookupId)));
            Assert.AreEqual("Weight > 150", heavy.ToSql());
        }

        [TestMethod]
        public void ExpressionInALongerTextKeepsItsPositions()
        {
            // Arrange
            const string Text = "FILTER Rank > 2 AND Pay < 3 END";

            // Act
            var syntax = SqlSyntaxTree.ParseExpression(Text, 7, 20, null);
            var broken = SqlSyntaxTree.ParseExpression(Text, 7, 13, null);

            // Assert
            Assert.AreEqual("Rank@7, Pay@20", string.Join(", ", syntax.Names.Select(n => n.LookupId + "@" + n.Start)));
            Assert.AreEqual(7, syntax.Root.Start);
            Assert.AreEqual(20, syntax.Root.Length);
            Assert.AreEqual(20, broken.Error.Start);
        }

        [TestMethod]
        public void CallsKeepTheirFunctionAndArguments()
        {
            // Arrange
            const string Expression = "1 + twice(Rank + 1)";

            // Act
            var call = (NodeWithId)SqlSyntaxTree.ParseExpression(Expression, Functions).Root.Children.OfType<NodeWithId>().Single();

            // Assert
            Assert.AreSame(Functions[0], call.Function);
            Assert.AreEqual("twice", call.FunctionName);
            Assert.AreEqual("Rank + 1", call.Arguments.Single().Text);
            Assert.AreEqual("Rank + 1", call.Arguments.Single().Expression.ToSql());
            Assert.AreEqual(Expression.IndexOf("Rank", System.StringComparison.Ordinal), call.Arguments.Single().Start);
            Assert.AreEqual(Expression.IndexOf("twice", System.StringComparison.Ordinal), call.Start);
            Assert.AreEqual("twice(Rank + 1)".Length, call.Length);
        }

        [TestMethod]
        public void ArgumentThatIsNotAnExpressionKeepsItsText()
        {
            // Act
            var syntax = SqlSyntaxTree.ParseExpression("twice(1 second)", Functions);
            var argument = ((NodeWithId)syntax.Root).Arguments.Single();

            // Assert
            Assert.AreEqual("1 second", argument.Text);
            Assert.IsNull(argument.Expression);
            Assert.AreSame(argument.Error, syntax.Error);
            Assert.AreEqual(6, argument.Start);
            Assert.AreEqual(8, argument.Length);
        }
    }
}
