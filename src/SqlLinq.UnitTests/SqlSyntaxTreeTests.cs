using System;
using System.Collections.Generic;
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
        [DataRow("SELECT Name FROM this WHERE nofunc(Rank) > 6", "nofunc")]
        [DataRow("SELECT Name FROM this WHERE twice(nofunc(Rank)) > 6", "nofunc")]
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

        [TestMethod]
        public void BindGivesEachCallTheFunctionOfItsName()
        {
            // Arrange
            var standIn = new ISqlFunction[] { new TestFunction("TWICE", arguments => throw new SqlException("not bound")) };
            var syntax = SqlSyntaxTree.Parse("SELECT twice(Rank) AS R, Name FROM this WHERE twice(Age + twice(Rank)) > abs(Rank)", standIn);
            var crew = new[] { new CrewMember { Name = "Ed", Rank = 6, Age = 40 }, new CrewMember { Name = "Kelly", Rank = 5, Age = -30 } };

            // Act
            var bound = syntax.Bind(Functions);
            var rows = bound.Compile<CrewMember, IDictionary<string, object>>().Execute(crew).ToList();

            // Assert
            Assert.AreEqual("Ed/12", string.Join(", ", rows.Select(r => r["Name"] + "/" + r["R"])));
            Assert.IsTrue(Calls(bound.Root).Where(f => f.Name == "twice").All(f => f == Functions[0]));
            Assert.AreEqual(3, Calls(bound.Root).Count(f => f == Functions[0]));
            Assert.IsTrue(Calls(syntax.Root).All(f => f != Functions[0]));
            Assert.AreEqual(syntax.Root.ToSql(), bound.Root.ToSql());
            Assert.AreEqual(syntax.Text, bound.Text);
            CollectionAssert.AreEqual(syntax.Names.ToList(), bound.Names.ToList());
            Assert.ThrowsExactly<SqlException>(() => syntax.Compile<CrewMember, IDictionary<string, object>>().Execute(crew).ToList());
        }

        [TestMethod]
        public void BindKeepsCallsItHasNoFunctionFor()
        {
            // Arrange
            var expression = SqlSyntaxTree.ParseExpression("twice(Rank) + abs(Age)", Functions).Root;
            var abs = expression.FindDescendants<NodeWithId>().Single(n => n.FunctionName == "abs").Function;

            // Act
            var bound = expression.Bind(Array.Empty<ISqlFunction>());

            // Assert
            Assert.AreNotSame(expression, bound);
            Assert.AreEqual(expression.ToSql(), bound.ToSql());
            CollectionAssert.AreEqual(new[] { Functions[0], abs }, Calls(bound).ToList());
            Assert.ThrowsExactly<InvalidOperationException>(() => SqlSyntaxTree.Parse("SELECT FROM").Bind(Functions));
            Assert.ThrowsExactly<ArgumentNullException>(() => expression.Bind(null));
        }

        [TestMethod]
        public void BindOfTwoFunctionsWithOneNameThrowsAsParseDoes()
        {
            // Arrange
            var twice = Functions.Concat(Functions).ToArray();
            var syntax = SqlSyntaxTree.Parse("SELECT twice(Rank) FROM this", Functions);

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() => SqlSyntaxTree.Parse("SELECT twice(Rank) FROM this", twice));
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Root.Bind(twice));
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Bind(twice));
        }

        [TestMethod]
        [DataRow("Rank > twice(Pay + 1) AND Name = 'Ed'", 0)]
        [DataRow("Rank > twice(Pay + 1) AND Name = 'Ed'", 9)]
        [DataRow("Rank > twice(Pay + 1) AND\n Name = 'Ed'", 4)]
        [DataRow("Rank > twice(Pay +", 3)]
        [DataRow("Rank > twice(Pay >) = 2", 5)]
        public void ShiftIsTheTreeOfTheTextItIsShiftedInto(string expression, int offset)
        {
            // Arrange
            var text = "WHERE" + new string(' ', offset) + "\n" + expression;
            var syntax = SqlSyntaxTree.ParseExpression(expression, Functions);
            var parsed = SqlSyntaxTree.ParseExpression(text, text.Length - expression.Length, expression.Length, Functions);

            // Act
            var shifted = syntax.Shift(text, text.Length - expression.Length);

            // Assert
            Assert.AreEqual(Positions(parsed), Positions(shifted));
            Assert.AreEqual(text, shifted.Text);
            Assert.AreEqual(expression, syntax.Text);
            Assert.AreEqual(Positions(SqlSyntaxTree.ParseExpression(expression, Functions)), Positions(syntax));
            Assert.ThrowsExactly<ArgumentNullException>(() => syntax.Shift(null, 0));
        }

        private static string Positions(SqlSyntaxTree syntax)
        {
            return string.Join(
                " | ",
                string.Join(", ", Nodes(syntax.Root).OfType<TerminalNode>().Select(t => t.Text + "@" + t.Start + "/" + t.Length + "/" + t.LineNumber + ":" + t.LinePosition)),
                string.Join(", ", Nodes(syntax.Root).OfType<ArgumentNode>().Select(a => a.Text + "@" + a.Start + "/" + a.Length + "/" + a.Error?.Start)),
                string.Join(", ", syntax.Names.Select(n => n.LookupId + "@" + n.Start + "/" + n.Length)),
                syntax.Root?.ToSql(),
                syntax.Error?.Message + "@" + syntax.Error?.Start + "/" + syntax.Error?.Length + "/" + syntax.Error?.Kind);
        }

        private static IEnumerable<SyntaxNode> Nodes(SyntaxNode node)
        {
            if (node is not NonTerminalNode parent)
            {
                return node == null ? [] : [node];
            }

            var arguments = (parent as NodeWithId)?.Arguments ?? (parent as ColumnSource)?.Arguments ?? [];

            return parent.Children.SelectMany(Nodes).Concat(arguments.SelectMany(a => Nodes(a.Expression).Prepend(a))).Prepend(parent);
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE Rank >", SqlErrorKind.Syntax)]
        [DataRow("SELECT * FROM this /* unclosed", SqlErrorKind.UnterminatedComment)]
        [DataRow("SELECT * FROM this WHERE Name = 'Ed", SqlErrorKind.UnterminatedString)]
        [DataRow("SELECT * FROM this WHERE Rank ~ 1", SqlErrorKind.Lexical)]
        [DataRow("SELECT Name FROM this WHERE twice(Rank > 6", SqlErrorKind.UnbalancedParentheses)]
        [DataRow("SELECT Name FROM this WHERE twice(Rank >) = 2", SqlErrorKind.Syntax)]
        [DataRow("SELECT Name FROM this WHERE nofunc(Rank) > 6", SqlErrorKind.UnknownName)]
        [DataRow("SELECT Name FROM this WHERE COUNT(*) > 1", SqlErrorKind.Semantic)]
        public void ParseErrorsSayWhatTheyAre(string sql, SqlErrorKind kind)
        {
            // Act
            var error = SqlSyntaxTree.Parse(sql, Functions).Error;

            // Assert
            Assert.AreEqual(kind, error.Kind);
        }

        [TestMethod]
        public void CompileAndRunErrorsSayWhatTheyAre()
        {
            // Arrange
            var crew = Orville.Crew();
            var dictionaries = Orville.CrewDictionaries();

            // Act
            var member = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<CrewMember>("Warp + 1"));
            var qualifier = Assert.ThrowsExactly<SqlException>(() => crew.Query<CrewMember, IDictionary<string, object>>("SELECT x.Name FROM this").ToList());
            var grouping = Assert.ThrowsExactly<SqlException>(() => crew.Query<CrewMember, IDictionary<string, object>>("SELECT Name, COUNT(*) FROM this").ToList());
            var arithmetic = Assert.ThrowsExactly<SqlException>(() => dictionaries.Query<IDictionary<string, object>, IDictionary<string, object>>("SELECT Name * 2 AS X FROM this").ToList());
            var placed = new SqlException("placed", SqlErrorKind.Semantic, 3, 4);

            // Assert
            Assert.AreEqual(SqlErrorKind.UnknownName, member.Kind);
            Assert.AreEqual(SqlErrorKind.UnknownName, qualifier.Kind);
            Assert.AreEqual(SqlErrorKind.Semantic, grouping.Kind);
            Assert.AreEqual(SqlErrorKind.Evaluation, arithmetic.Kind);
            Assert.AreEqual(SqlErrorKind.Other, new SqlException("plain", 1, 2).Kind);
            Assert.AreEqual("placed/Semantic/3/4", placed.Message + "/" + placed.Kind + "/" + placed.Start + "/" + placed.Length);
        }

        private static IEnumerable<ISqlFunction> Calls(NonTerminalNode node)
        {
            var calls = node.FindDescendants<NonTerminalNode>().Prepend(node).Select(n => n is NodeWithId call ? call.Function : (n as ColumnSource)?.Function);
            var arguments = node.FindDescendants<NonTerminalNode>().Prepend(node).SelectMany(n => (n as NodeWithId)?.Arguments ?? (n as ColumnSource)?.Arguments ?? Enumerable.Empty<ArgumentNode>());

            return calls.Where(f => f != null).Concat(arguments.Where(a => a.Expression != null).SelectMany(a => Calls(a.Expression)));
        }
    }
}
