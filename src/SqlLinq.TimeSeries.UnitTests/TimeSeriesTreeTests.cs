using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using LinqStatistics;
using MathNet.Numerics.Statistics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.TimeSeries.UnitTests
{
    [TestClass]
    public class TimeSeriesTreeTests
    {
        private static readonly string[] Operations = ["Values.Read", "Enumerable.Where", "Enumerable.GroupBy", "Enumerable.OrderByDescending", "Enumerable.Range", "Enumerable.ToLookup", "Enumerable.Zip"];

        private static List<Sample> GetSamples()
        {
            return
            [
                new Sample { Id = 1, Time = 0.0, DriveSpin = 850, PlasmaPressure = 30.5, FuelMix = 14.7, DriveStage = 0, Quantum = false, Note = "Union Point" },
                new Sample { Id = 2, Time = 0.5, DriveSpin = 2500, PlasmaPressure = 55.0, FuelMix = 14.2, DriveStage = 1, Quantum = false, Note = "Moclus" },
                new Sample { Id = 3, Time = 1.0, DriveSpin = 4800, PlasmaPressure = 85.2, FuelMix = 12.9, DriveStage = 2, Quantum = false, Note = "Xelaya" },
                new Sample { Id = 4, Time = 1.5, DriveSpin = 6200, PlasmaPressure = 98.7, FuelMix = 12.1, DriveStage = 2, Quantum = true, Note = "Krill space" },
                new Sample { Id = 5, Time = 2.0, DriveSpin = 7900, PlasmaPressure = 101.3, FuelMix = 11.8, DriveStage = 3, Quantum = true, Note = null },
                new Sample { Id = 6, Time = 2.5, DriveSpin = 3100, PlasmaPressure = 40.0, FuelMix = null, DriveStage = 3, Quantum = false, Note = "Kaylon" },
                new Sample { Id = 7, Time = 3.0, DriveSpin = 5600, PlasmaPressure = 92.0, FuelMix = 12.5, DriveStage = 4, Quantum = true, Note = "" },
                new Sample { Id = 8, Time = 3.5, DriveSpin = 850, PlasmaPressure = 29.9, FuelMix = 14.7, DriveStage = 0, Quantum = false, Note = "Union Point" },
            ];
        }

        private static TimeSpan Time(Sample sample)
        {
            return TimeSpan.FromSeconds(sample.Time);
        }

        private static string Render(IEnumerable<IDictionary<string, object>> rows)
        {
            return string.Join("; ", rows.Select(r => string.Join(", ", r.Select(c => c.Key + "=" + Convert.ToString(c.Value, CultureInfo.InvariantCulture)))));
        }

        private static List<Expression> Nodes(Expression tree)
        {
            var nodes = new Collector();
            nodes.Visit(tree);

            return nodes.All;
        }

        private static MethodInfo Operation(string nrql, string name)
        {
            var plan = TimeSeriesSyntaxTree.Parse(nrql).Compile<Sample>();

            return Nodes(plan.Expression).OfType<MethodCallExpression>().Select(c => c.Method).First(m => m.Name == name);
        }

        [TestMethod]
        [DataRow("SELECT max(DriveSpin) AS Top, count(*) FROM shipslog WHERE DriveStage > 1 FACET Quantum TIMESERIES 1 second")]
        [DataRow("SELECT max(DriveSpin) - min(DriveSpin) AS Spread FROM shipslog FACET buckets(PlasmaPressure, 120, 3) SESSION 600 ms")]
        [DataRow("SELECT count(*) AS N, latest(Note) FROM shipslog SINCE 1 COMPARE WITH 1 second ago")]
        [DataRow("SELECT Id, DriveSpin * 2 AS Twice FROM shipslog WHERE Quantum LIMIT 2")]
        public void PlanIsOneTreeThatRunsTheQuery(string nrql)
        {
            // Arrange
            var plan = TimeSeriesSyntaxTree.Parse(nrql).Compile<Sample>();

            // Act
            var executed = Render(plan.Execute(GetSamples(), Time));
            var invoked = Render(plan.Expression.Compile()(GetSamples(), Time, CancellationToken.None));

            // Assert
            Assert.AreEqual(executed, invoked);
            Assert.AreNotEqual(string.Empty, executed);
        }

        [TestMethod]
        public void TreeHoldsEveryExpressionOfTheQuery()
        {
            // Arrange
            var plan = TimeSeriesSyntaxTree.Parse("SELECT max(DriveSpin) AS Top, average(FuelMix) FROM shipslog WHERE DriveStage > 1 FACET Quantum, cases(PlasmaPressure < 60 AS 'low', PlasmaPressure >= 60 AS 'high') TIMESERIES 1 second").Compile<Sample>();

            // Act
            var nodes = Nodes(plan.Expression);
            var members = nodes.OfType<MemberExpression>().Where(m => m.Member.DeclaringType == typeof(Sample)).Select(m => m.Member.Name).Distinct().OrderBy(n => n).ToList();
            var calls = nodes.OfType<MethodCallExpression>().Select(c => c.Method.DeclaringType.Name + "." + c.Method.Name).Distinct().ToList();

            // Assert
            Assert.AreEqual("DriveSpin, DriveStage, FuelMix, PlasmaPressure, Quantum", string.Join(", ", members));
            Assert.IsFalse(nodes.OfType<ConstantExpression>().Any(c => c.Value is Delegate));
            Assert.IsTrue(nodes.OfType<InvocationExpression>().All(i => i.Expression is ParameterExpression));
            CollectionAssert.IsSubsetOf(Operations, calls);
        }

        [TestMethod]
        public void ReadPairsEachItemWithItsTimeAndObservesCancellation()
        {
            // Arrange
            var read = Operation("SELECT count(*) FROM shipslog", "Read");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // Act
            var rows = (List<(TimeSpan Time, Sample Item)>)read.Invoke(null, [GetSamples().Take(2), (Func<Sample, TimeSpan>)Time, CancellationToken.None]);
            var e = Assert.ThrowsExactly<TargetInvocationException>(() => read.Invoke(null, [GetSamples(), (Func<Sample, TimeSpan>)Time, cancellation.Token]));

            // Assert
            Assert.AreEqual("00:00:00/1, 00:00:00.5000000/2", string.Join(", ", rows.Select(r => r.Time + "/" + r.Item.Id)));
            Assert.IsInstanceOfType<OperationCanceledException>(e.InnerException);
        }

        [TestMethod]
        [DataRow("SINCE 2 UNTIL 5", "2, 3, 4")]
        [DataRow("SINCE 0.5 UNTIL 3", "0.5, 1.5, 2.5")]
        [DataRow("SINCE 5 UNTIL 6", "5")]
        [DataRow("SINCE 5 UNTIL 5", "")]
        [DataRow("SINCE 5 UNTIL 4", "")]
        public void BucketsAreEveryIndexFromFirstToLast(string window, string expected)
        {
            // Arrange
            var nrql = "SELECT count(*) FROM shipslog " + window + " TIMESERIES 1 second";
            var range = Operation(nrql, "Range");

            // Act
            var rows = TimeSeriesSyntaxTree.Parse(nrql).Compile<Sample>().Execute(GetSamples(), Time);

            // Assert
            Assert.AreEqual(typeof(Enumerable), range.DeclaringType);
            Assert.AreEqual(expected, string.Join(", ", rows.Select(r => ((TimeSpan)r["BeginTime"]).TotalSeconds.ToString(CultureInfo.InvariantCulture))));
        }

        [TestMethod]
        public void DerivativeIsTheSlopeOfTheFittedLine()
        {
            // Arrange
            var fit = Operation("SELECT derivative(DriveSpin, 1 second) FROM shipslog", "LeastSquares");
            var plan = TimeSeriesSyntaxTree.Parse("SELECT derivative(V, 1 minute) AS D FROM log").Compile<IDictionary<string, object>>();
            var line = new object[] { 1, 3.0, 5m }.Select((v, i) => (IDictionary<string, object>)new Dictionary<string, object> { ["T"] = i, ["V"] = v }).ToList();
            var sameTime = new object[] { 1, 2 }.Select(v => (IDictionary<string, object>)new Dictionary<string, object> { ["T"] = 0, ["V"] = v }).ToList();
            Func<IDictionary<string, object>, TimeSpan> time = r => TimeSpan.FromSeconds((int)r["T"]);

            // Act
            var perMinute = (double)plan.Execute(line, time).Single()["D"];
            var single = plan.Execute(line.Take(1), time).Single()["D"];
            var together = plan.Execute(sameTime, time).Single()["D"];

            // Assert
            Assert.AreEqual(typeof(EnumerableStats), fit.DeclaringType);
            Assert.AreEqual(120.0, perMinute, 1e-9);
            Assert.IsNull(single);
            Assert.IsNull(together);
        }

        [TestMethod]
        public void PercentileAndDeviationReadNumbersAsDoubles()
        {
            // Arrange
            var quantile = Operation("SELECT percentile(DriveSpin, 50) FROM shipslog", "QuantileCustom");
            var deviation = Operation("SELECT stddev(DriveSpin) FROM shipslog", "StandardDeviation");
            var plan = TimeSeriesSyntaxTree.Parse("SELECT median(V) AS M, percentile(V, 100) AS P, stddev(V) AS S FROM log").Compile<IDictionary<string, object>>();
            var values = new object[] { 4, 1.0, 3m, 2L }.Select(v => (IDictionary<string, object>)new Dictionary<string, object> { ["V"] = v }).ToList();

            // Act
            var row = plan.Execute(values, r => TimeSpan.Zero).Single();
            var one = plan.Execute(values.Take(1), r => TimeSpan.Zero).Single();

            // Assert
            Assert.AreEqual(typeof(SortedArrayStatistics), quantile.DeclaringType);
            Assert.AreEqual(typeof(EnumerableStats), deviation.DeclaringType);
            Assert.AreEqual(2.5, row["M"]);
            Assert.AreEqual(4.0, row["P"]);
            Assert.AreEqual(Math.Sqrt(5.0 / 3), (double)row["S"], 1e-12);
            Assert.IsNull(one["S"]);
        }

        [TestMethod]
        public void SortKeyRanksNumbersByValue()
        {
            // Arrange
            var sortKey = Operation("SELECT count(*) FROM shipslog FACET Quantum", "SortKey");

            // Act
            var keys = new object[] { 3, 2.5m, "x", null }.Select(v => sortKey.Invoke(null, [v])).ToList();

            // Assert
            Assert.AreEqual("3:Double, 2.5:Double, x:String, :", string.Join(", ", keys.Select(k => Convert.ToString(k, CultureInfo.InvariantCulture) + ":" + k?.GetType().Name)));
        }

        [TestMethod]
        public void BucketPlacesANumberBetweenItsBoundaries()
        {
            // Arrange
            var bucket = Operation("SELECT count(*) FROM shipslog FACET buckets(DriveSpin, 8000, 4)", "Bucket");
            var boundaries = new[] { 2000d, 4000d, 6000d };
            var labels = new[] { "< 2000", "2000 - < 4000", "4000 - < 6000", ">= 6000" };

            // Act
            var placed = new object[] { 0, 2000L, 3999.5, 6000m, double.NaN, null }.Select(v => bucket.Invoke(null, [v, boundaries, labels]));
            var e = Assert.ThrowsExactly<TargetInvocationException>(() => bucket.Invoke(null, ["x", boundaries, labels]));

            // Assert
            Assert.AreEqual("< 2000|2000 - < 4000|2000 - < 4000|>= 6000||", string.Join("|", placed));
            Assert.AreEqual("buckets can't use String values because they aren't numbers.", e.InnerException.Message);
        }

        private sealed class Collector : ExpressionVisitor
        {
            public List<Expression> All { get; } = new List<Expression>();

            public override Expression Visit(Expression node)
            {
                if (node != null)
                {
                    All.Add(node);
                }

                return base.Visit(node);
            }
        }
    }
}
