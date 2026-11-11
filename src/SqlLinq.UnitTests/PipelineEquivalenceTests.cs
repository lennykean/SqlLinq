using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class PipelineEquivalenceTests
    {
        private static readonly ISqlFunction[] Functions =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
        ];

        private static string Render(IEnumerable<IDictionary<string, object>> rows)
        {
            return string.Join("; ", rows.Select(r => string.Join(", ", r.Select(c => c.Key + "=" + Convert.ToString(c.Value, CultureInfo.InvariantCulture)))));
        }

        private static string Render(IEnumerable<CrewMember> rows)
        {
            return string.Join("; ", rows.Select(c => c.Name + "/" + c.Rank));
        }

        private static string Outcome(Func<string> run)
        {
            try
            {
                return run();
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        private static Dictionary<string, Type> Types(IDictionary<string, IEnumerable> tables)
        {
            return tables.ToDictionary(t => t.Key, t => t.Value.GetType().GetGenericArguments()[0]);
        }

        [TestMethod]
        [DataRow("SELECT * FROM this")]
        [DataRow("SELECT * FROM this WHERE Rank > 2 ORDER BY Age DESC")]
        [DataRow("SELECT DISTINCT * FROM this")]
        [DataRow("SELECT * FROM this WHERE Name LIKE 'K%' OR Species IN ('Moclan', 'Kaylon')")]
        [DataRow("SELECT * FROM this WHERE Age BETWEEN 35 AND 45 AND NOT Species = 'Moclan'")]
        [DataRow("SELECT * FROM this WHERE ShipId IS NULL")]
        [DataRow("SELECT * FROM this WHERE abs(Rank - 3) = 1 ORDER BY Rank, Name DESC")]
        [DataRow("SELECT * FROM this WHERE Rank = (SELECT max(Rank) FROM this)")]
        [DataRow("SELECT * FROM this WHERE Age > (SELECT avg(c.Age) FROM this c WHERE c.Species = this.Species)")]
        public void TypedRowsAreTheSame(string sql)
        {
            // Act
            var old = Outcome(() => Render(Orville.Crew().Query(sql)));
            var pipeline = Outcome(() => Render(SqlSyntaxTree.Parse(sql).Compile<CrewMember, CrewMember>().Execute(Orville.Crew())));

            // Assert
            Assert.AreEqual(old, pipeline);
            Assert.DoesNotContain("Exception: ", old);
        }

        [TestMethod]
        [DataRow("SELECT Name, Rank * 2 AS Twice, Pay FROM this ORDER BY Name")]
        [DataRow("SELECT Species, COUNT(*) AS N FROM this GROUP BY Species ORDER BY Species")]
        [DataRow("SELECT Species, AVG(Age) AS A, MIN(Age) AS Lo, MAX(Age) AS Hi, SUM(Rank) AS S FROM this GROUP BY Species HAVING COUNT(*) > 1")]
        [DataRow("SELECT StDev(Age) AS Sd, StDevP(Age) AS Sdp, Var(Age) AS V, VarP(Age) AS Vp FROM this")]
        [DataRow("SELECT COUNT(DISTINCT Species) AS Kinds, COUNT(ShipId) AS Assigned FROM this")]
        [DataRow("SELECT Name FROM this WHERE Rank > 3 UNION SELECT Species AS Name FROM this")]
        [DataRow("SELECT Name, Age FROM this c WHERE c.Rank IN (SELECT Rank FROM this WHERE Age < 35)")]
        [DataRow("SELECT Species, MAX(Rank) AS Top FROM this GROUP BY Species HAVING MAX(Rank) > 2 ORDER BY Top DESC")]
        public void ProjectedRowsAreTheSame(string sql)
        {
            // Act
            var old = Outcome(() => Render(Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql)));
            var pipeline = Outcome(() => Render(SqlSyntaxTree.Parse(sql).Compile<CrewMember, IDictionary<string, object>>().Execute(Orville.Crew())));

            // Assert
            Assert.AreEqual(old, pipeline);
            Assert.DoesNotContain("Exception: ", old);
        }

        [TestMethod]
        [DataRow("SELECT Name, Rank FROM this WHERE Species = 'Human' ORDER BY Rank")]
        [DataRow("SELECT Species, COUNT(*) AS N FROM this GROUP BY Species ORDER BY N DESC, Species")]
        public void CompiledQueryRowsAreTheSame(string sql)
        {
            // Arrange
            var query = new Query<CrewMember, IDictionary<string, object>>(sql);
            query.Compile();

            // Act
            var old = Render(query.Evaluate(Orville.Crew()));
            var pipeline = Render(SqlSyntaxTree.Parse(sql).Compile<CrewMember, IDictionary<string, object>>().Execute(Orville.Crew()));

            // Assert
            Assert.AreEqual(old, pipeline);
        }

        [TestMethod]
        public void CompiledQueryReadsItsOneSourceAsInVersionTwoOne()
        {
            // Arrange
            const string Sql = "SELECT Name, Rank - (SELECT MIN(Rank) FROM crew) AS AboveLowest FROM crew WHERE Rank > 3";
            var query = new Query<CrewMember, IDictionary<string, object>>(Sql);
            query.Compile();

            // Act
            var compiled = Render(query.Evaluate(Orville.Crew()));
            var extension = Render(Orville.Crew().Query<CrewMember, IDictionary<string, object>>(Sql));

            // Assert
            Assert.AreEqual("Name=Ed Mercer, AboveLowest=1; Name=Kelly Grayson, AboveLowest=0", compiled);
            Assert.AreEqual("Name=Ed Mercer, AboveLowest=3; Name=Kelly Grayson, AboveLowest=2", extension);
        }

        [TestMethod]
        public void ScalarIsTheSingleRow()
        {
            // Act
            var old = Orville.Crew().QueryScalar<CrewMember, int>("SELECT max(Rank) FROM this");
            var pipeline = SqlSyntaxTree.Parse("SELECT max(Rank) FROM this").Compile<CrewMember, int>().Execute(Orville.Crew()).Single();

            // Assert
            Assert.AreEqual(old, pipeline);
        }

        [TestMethod]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id ORDER BY c.Name")]
        [DataRow("SELECT s.Name, c.Name AS Member FROM ships s LEFT JOIN crew c ON s.Id = c.ShipId ORDER BY s.Name, c.Name")]
        [DataRow("SELECT s.Name, k.Role FROM ships s JOIN classes k ON s.Class = k.Class")]
        [DataRow("SELECT c.Name, p.Gravity FROM crew c JOIN planets p ON c.Species = p.Species WHERE p.Moons > 0")]
        [DataRow("SELECT Name FROM ships WHERE Id IN (SELECT ShipId FROM crew)")]
        [DataRow("SELECT Name FROM ships s WHERE EXISTS (SELECT * FROM crew c WHERE c.ShipId = s.Id)")]
        [DataRow("SELECT Name FROM ships s WHERE s.Id = (SELECT MAX(c.ShipId) FROM crew c WHERE c.ShipId = s.Id)")]
        [DataRow("SELECT Name FROM crew UNION SELECT Name FROM ships")]
        [DataRow("SELECT Species, COUNT(*) AS N FROM crew GROUP BY Species ORDER BY Species")]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = c.Rank")]
        [DataRow("SELECT Name FROM starbases")]
        public void TablesRowsAreTheSame(string sql)
        {
            // Act
            var old = Outcome(() => Render(Orville.Tables().Query(sql)));
            var pipeline = Outcome(() => Render(SqlSyntaxTree.Parse(sql).Compile<IDictionary<string, object>>(Types(Orville.Tables())).Execute(Orville.Tables())));
            var dictionaries = Outcome(() => Render(Orville.DictionaryTables().Query(sql)));
            var dictionaryPipeline = Outcome(() => Render(SqlSyntaxTree.Parse(sql).Compile<IDictionary<string, object>>(Types(Orville.DictionaryTables())).Execute(Orville.DictionaryTables())));

            // Assert
            Assert.AreEqual(old, pipeline);
            Assert.AreEqual(dictionaries, dictionaryPipeline);
        }

        [TestMethod]
        public void TwoSourceRowsAreTheSame()
        {
            // Arrange
            const string Sql = "SELECT this.Name, that.Name AS Ship FROM this JOIN that ON this.ShipId = that.Id ORDER BY this.Name";
            var tables = new Dictionary<string, IEnumerable> { ["this"] = Orville.Crew(), ["that"] = Orville.Ships() };

            // Act
            var old = Render(Orville.Crew().Query<CrewMember, Ship, IDictionary<string, object>>(Sql, Orville.Ships()));
            var pipeline = Render(SqlSyntaxTree.Parse(Sql).Compile<IDictionary<string, object>>(Types(tables)).Execute(tables));

            // Assert
            Assert.AreEqual(old, pipeline);
        }

        [TestMethod]
        [DataRow("SELECT Name, twice(Rank) AS R FROM this WHERE twice(Rank) > 6 ORDER BY Name")]
        [DataRow("SELECT Species, MAX(twice(Age)) AS Oldest FROM this GROUP BY Species ORDER BY Species")]
        [DataRow("SELECT Name FROM this WHERE twice(Rank, Age) > 6")]
        [DataRow("SELECT Name FROM this WHERE twice(Rank > 6")]
        public void CustomFunctionRowsAreTheSame(string sql)
        {
            // Act
            var old = Outcome(() => Render(Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql, Functions)));
            var pipeline = Outcome(() => Render(SqlSyntaxTree.Parse(sql, Functions).Compile<CrewMember, IDictionary<string, object>>().Execute(Orville.Crew())));

            // Assert
            Assert.AreEqual(old, pipeline);
        }

        [TestMethod]
        [DataRow("SELECT FROM this")]
        [DataRow("SELECT * FROM this WHERE Rank >")]
        [DataRow("SELECT * FROM this /* unclosed")]
        [DataRow("SELECT * FROM this WHERE Name = 'Ed")]
        [DataRow("SELECT Warp FROM this")]
        [DataRow("SELECT * FROM this WHERE Warp > 1")]
        [DataRow("SELECT * FROM this ORDER BY Warp")]
        [DataRow("SELECT Species FROM this GROUP BY Warp")]
        [DataRow("SELECT nope(Rank) FROM this")]
        [DataRow("SELECT * FROM this WHERE Name LIKE Rank")]
        public void ErrorsAreTheSame(string sql)
        {
            // Act
            var old = Outcome(() => Render(Orville.Crew().Query<CrewMember, IDictionary<string, object>>(sql)));
            var pipeline = Outcome(() => Render(SqlSyntaxTree.Parse(sql).Compile<CrewMember, IDictionary<string, object>>().Execute(Orville.Crew())));

            // Assert
            Assert.AreEqual(old, pipeline);
            Assert.IsFalse(old.StartsWith(nameof(NullReferenceException), StringComparison.Ordinal), old);
        }

        [TestMethod]
        [DataRow("Rank * 2 + abs(Age - 40)")]
        [DataRow("Name LIKE '%a%' AND ShipId IS NOT NULL")]
        [DataRow("Rank >")]
        [DataRow("Warp + 1")]
        public void ExpressionValuesAreTheSame(string expression)
        {
            // Act
            var old = Outcome(() => string.Join(", ", Orville.Crew().Select(SqlExpression.Compile<CrewMember>(expression))));
            var pipeline = Outcome(() =>
            {
                var syntax = SqlSyntaxTree.ParseExpression(expression);
                if (syntax.Error != null)
                {
                    throw syntax.Error;
                }

                return string.Join(", ", Orville.Crew().Select(SqlExpression.Compile<CrewMember>((ExpressionNode)syntax.Root)));
            });

            // Assert
            Assert.AreEqual(old, pipeline);
        }
    }
}
