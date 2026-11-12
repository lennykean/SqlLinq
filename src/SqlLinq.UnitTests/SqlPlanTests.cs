using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class SqlPlanTests
    {
        private static readonly Dictionary<string, Type> CrewAndShips = new Dictionary<string, Type> { ["crew"] = typeof(CrewMember), ["ships"] = typeof(Ship) };

        [TestMethod]
        public void PlanRunsOverEachSourceItIsGiven()
        {
            // Arrange
            var plan = SqlSyntaxTree.Parse("SELECT * FROM this WHERE Rank > 2").Compile<CrewMember, CrewMember>();
            var crew = Orville.Crew();

            // Act
            var all = plan.Execute(crew).ToList();
            var firstTwo = plan.Execute(crew.Take(2)).ToList();

            // Assert
            CollectionAssert.AreEqual(crew.Where(c => c.Rank > 2).ToList(), all);
            CollectionAssert.AreEqual(crew.Take(2).Where(c => c.Rank > 2).ToList(), firstTwo);
        }

        [TestMethod]
        public void PlanIsOneTreeThatRunsTheStatement()
        {
            // Arrange
            var plan = SqlSyntaxTree.Parse("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank > 2 ORDER BY c.Name").Compile<IDictionary<string, object>>(CrewAndShips);
            var tables = new Dictionary<string, IEnumerable> { ["crew"] = Orville.Crew(), ["ships"] = Orville.Ships() };

            // Act
            var executed = plan.Execute(tables).Select(r => r["Name"] + "/" + r["Ship"]);
            var invoked = plan.Expression.Compile()([Orville.Crew(), Orville.Ships()]).Select(r => r["Name"] + "/" + r["Ship"]);

            // Assert
            Assert.AreEqual("Bortus/Orville, Ed Mercer/Orville, Kelly Grayson/Orville, Talla Keyali/Roosevelt", string.Join(", ", executed));
            Assert.AreEqual(string.Join(", ", executed), string.Join(", ", invoked));
            StringAssert.Contains(plan.Expression.ToString(), "Where(");
            StringAssert.Contains(plan.Expression.ToString(), "Join(");
        }

        [TestMethod]
        public void ColumnsAreTheSelectList()
        {
            // Act
            var projection = SqlSyntaxTree.Parse("SELECT Name, Rank * 2 AS R, COUNT(*) FROM this GROUP BY Name, Rank").Compile<CrewMember, IDictionary<string, object>>();
            var star = SqlSyntaxTree.Parse("SELECT * FROM this").Compile<CrewMember, CrewMember>();

            // Assert
            Assert.AreEqual("Name, R, CountStar", string.Join(", ", projection.Columns));
            Assert.AreEqual("Name, Species, Rank, Age, Pay, ShipId", string.Join(", ", star.Columns));
            Assert.AreEqual("SELECT * FROM this", star.Syntax.Text);
        }

        [TestMethod]
        public void ColumnsOfAStarOverDictionariesAreKnownOnlyWhenRead()
        {
            // Act
            var plan = SqlSyntaxTree.Parse("SELECT * FROM this").Compile<IDictionary<string, object>, IDictionary<string, object>>();

            // Assert
            Assert.IsEmpty(plan.Columns);
        }

        [TestMethod]
        public void CompileThrowsTheParseError()
        {
            // Arrange
            var syntax = SqlSyntaxTree.Parse("SELECT FROM this");

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => syntax.Compile<CrewMember, CrewMember>());

            // Assert
            Assert.AreSame(syntax.Error, e);
        }

        [TestMethod]
        public void CompileFindsUnknownMembersBeforeAnyRowIsRead()
        {
            // Arrange
            var syntax = SqlSyntaxTree.Parse("SELECT Warp FROM this");

            // Act & Assert
            Assert.IsNull(syntax.Error);
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Compile<CrewMember, IDictionary<string, object>>());
        }

        [TestMethod]
        public void CompileErrorOfAnExpressionCarriesItsTextAndPosition()
        {
            // Arrange
            const string Text = "FILTER Rank > 2 AND Warp<3 END";
            var syntax = SqlSyntaxTree.ParseExpression(Text, 7, 19, null);

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<CrewMember>((ExpressionNode)syntax.Root));

            // Assert
            Assert.AreEqual(7, e.Start);
            Assert.AreEqual(19, e.Length);
            Assert.EndsWith("in expression\n\tRank > 2 AND Warp<3", e.Message);
            Assert.IsInstanceOfType<ArgumentException>(e.InnerException);
        }

        [TestMethod]
        public void CompileOfAnExpressionIsRefused()
        {
            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => SqlSyntaxTree.ParseExpression("Rank > 2").Compile<CrewMember, CrewMember>());
        }

        [TestMethod]
        public void TablesAreFoundByNameInAnyCase()
        {
            // Arrange
            var plan = SqlSyntaxTree.Parse("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY c.Name").Compile<IDictionary<string, object>>(CrewAndShips);
            var tables = new Dictionary<string, IEnumerable> { ["CREW"] = Orville.Crew(), ["Ships"] = Orville.Ships() };

            // Act
            var rows = plan.Execute(tables).Select(r => r["Name"] + "/" + r["Ship"]);

            // Assert
            Assert.AreEqual("Bortus/Orville, Ed Mercer/Orville, Gordon Malloy/Orville, Kelly Grayson/Orville, Talla Keyali/Roosevelt", string.Join(", ", rows));
        }

        [TestMethod]
        public void NullTablesAreEmptyAndMissingTablesAreRefused()
        {
            // Arrange
            var plan = SqlSyntaxTree.Parse("SELECT s.Name FROM ships s LEFT JOIN crew c ON s.Id = c.ShipId").Compile<IDictionary<string, object>>(CrewAndShips);

            // Act
            var rows = plan.Execute(new Dictionary<string, IEnumerable> { ["crew"] = null, ["ships"] = Orville.Ships() }).ToList();

            // Assert
            Assert.HasCount(3, rows);
            Assert.ThrowsExactly<ArgumentException>(() => plan.Execute(new Dictionary<string, IEnumerable> { ["ships"] = Orville.Ships() }));
            Assert.ThrowsExactly<InvalidOperationException>(() => plan.Execute(Orville.Ships()));
        }

        [TestMethod]
        public void CancellingStopsTheReadingOfTheTables()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var plan = SqlSyntaxTree.Parse("SELECT Name FROM crew WHERE Rank > 2").Compile<IDictionary<string, object>>(CrewAndShips);
            var rows = plan.Execute(new Dictionary<string, IEnumerable> { ["crew"] = Orville.Crew(), ["ships"] = Orville.Ships() }, cancellation.Token);
            cancellation.Cancel();

            // Act
            var e = Assert.ThrowsExactly<OperationCanceledException>(() => rows.ToList());

            // Assert
            Assert.AreEqual(cancellation.Token, e.CancellationToken);
        }

        [TestMethod]
        public void OnePlanRunsFromManyThreads()
        {
            // Arrange
            var plan = SqlSyntaxTree.Parse("SELECT Species, COUNT(*) AS N FROM this GROUP BY Species ORDER BY Species").Compile<CrewMember, IDictionary<string, object>>();

            // Act
            var results = Enumerable.Range(0, 16).AsParallel().Select(_ => string.Join(", ", plan.Execute(Orville.Crew()).Select(r => r["Species"] + "=" + r["N"]))).Distinct().ToList();

            // Assert
            Assert.AreEqual("Human=3, Kaylon=1, Moclan=1, Xelayan=1", results.Single());
        }

        [TestMethod]
        public void InheritedAndExplicitInterfaceMembersBind()
        {
            // Arrange
            var frames = new List<Frame> { new Frame { Rpm = 800, Gear = 1, Offset = TimeSpan.FromSeconds(1) }, new Frame { Rpm = 6000, Gear = 3, Vtec = true, Offset = TimeSpan.FromSeconds(2) } };

            // Act
            var inherited = frames.Query<IFrame, IDictionary<string, object>>("SELECT offset, Rpm FROM this WHERE Rpm > 1000").Single();
            var hidden = frames.Query<Frame, IDictionary<string, object>>("SELECT Hidden FROM this WHERE Hidden > 1000").Single();

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(2) + "/6000", inherited["offset"] + "/" + inherited["Rpm"]);
            Assert.AreEqual(3000.0, hidden["Hidden"]);
        }

        [TestMethod]
        public void TypedOperatorsReadMembersUnboxed()
        {
            // Arrange
            var plan = SqlSyntaxTree.Parse("SELECT * FROM this WHERE Rpm * 2 > Gear AND Vtec AND Map >= 30").Compile<Frame, Frame>();
            var boxing = new Boxing();

            // Act
            boxing.Visit(plan.Expression);

            // Assert
            Assert.IsEmpty(boxing.Members);
        }

        private interface IStamped
        {
            TimeSpan Offset { get; }
        }

        private interface IFrame : IStamped
        {
            double Rpm { get; }
        }

        private interface IHidden
        {
            double Hidden { get; }
        }

        private sealed class Frame : IFrame, IHidden
        {
            public double Rpm { get; set; }

            public double? Map { get; set; }

            public int Gear { get; set; }

            public bool Vtec { get; set; }

            public TimeSpan Offset { get; set; }

            double IHidden.Hidden => Rpm / 2;
        }

        private sealed class Boxing : System.Linq.Expressions.ExpressionVisitor
        {
            internal List<string> Members { get; } = [];

            protected override System.Linq.Expressions.Expression VisitUnary(System.Linq.Expressions.UnaryExpression node)
            {
                if (node.NodeType == System.Linq.Expressions.ExpressionType.Convert && node.Type == typeof(object) && node.Operand is System.Linq.Expressions.MemberExpression member && member.Member.DeclaringType == typeof(Frame))
                {
                    Members.Add(member.Member.Name);
                }

                return base.VisitUnary(node);
            }
        }
    }
}
