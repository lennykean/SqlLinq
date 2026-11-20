using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries.UnitTests
{
    [TestClass]
    public class TimeSeriesPipelineTests
    {
        private static readonly ISqlFunction[] Functions =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(double)), Expression.Constant(2.0))),
        ];

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

        private static string Outcome(Func<IEnumerable<IDictionary<string, object>>> run)
        {
            try
            {
                return string.Join("; ", run().Select(r => string.Join(", ", r.Select(c => c.Key + "=" + Convert.ToString(c.Value, CultureInfo.InvariantCulture)))));
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        [TestMethod]
        [DataRow("SELECT Id, PlasmaPressure * 2 AS Double, Note FROM shipslog WHERE DriveStage > 0 LIMIT 4")]
        [DataRow("SELECT count(*), count(FuelMix), sum(DriveSpin), average(DriveSpin), avg(FuelMix), min(Note), max(PlasmaPressure) FROM shipslog")]
        [DataRow("SELECT stddev(DriveSpin), uniqueCount(DriveStage), median(PlasmaPressure), percentile(DriveSpin, 90), latest(Note), earliest(Note) FROM shipslog")]
        [DataRow("SELECT percentile(DriveSpin, 5, 50, 95) AS P, percentile(PlasmaPressure, 25, 75) FROM shipslog")]
        [DataRow("SELECT rate(count(*), 1 second) AS PerSecond, rate(sum(DriveSpin), 1 minute), derivative(DriveSpin, 1 second) AS Slope FROM shipslog")]
        [DataRow("SELECT max(DriveSpin) - min(DriveSpin) AS Spread, round(average(FuelMix) * 10) / 10 FROM shipslog")]
        [DataRow("SELECT count(*) AS N, Quantum FROM shipslog FACET Quantum, DriveStage > 1 AS High LIMIT 3")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET buckets(DriveSpin, 8000, 4) AS Band, cases(FuelMix < 13 AS 'rich', FuelMix >= 13 AS 'lean')")]
        [DataRow("SELECT average(DriveSpin) FROM shipslog SINCE 2 seconds ago UNTIL 3500 ms TIMESERIES 500 ms")]
        [DataRow("SELECT max(DriveSpin) AS Top FROM shipslog FACET Quantum SESSION 600 ms")]
        [DataRow("SELECT count(*) AS N FROM shipslog SINCE 1 UNTIL 3 TIMESERIES 1 second COMPARE WITH 1 second ago")]
        [DataRow("SELECT Id FROM shipslog WHERE Note LIKE 'K%' OR Note IS NULL")]
        [DataRow("select COUNT(*) as n from [ships log] where drivestage in (2, 3) facet quantum since 1 limit 1")]
        public void RowsAreTheSame(string nrql)
        {
            // Act
            var query = Outcome(() => new TimeSeriesQuery<Sample>(nrql, Time).Evaluate(GetSamples()));
            var extension = Outcome(() => GetSamples().TimeSeriesQuery(nrql, Time));
            var pipeline = Outcome(() => TimeSeriesSyntaxTree.Parse(nrql).Rewrite().Compile<Sample>().Execute(GetSamples(), Time));

            // Assert
            Assert.AreEqual(query, pipeline);
            Assert.AreEqual(extension, pipeline);
            Assert.DoesNotContain("Exception: ", pipeline);
        }

        [TestMethod]
        [DataRow("SELECT count(*) FROM shipslog WHERE Warp > 1")]
        [DataRow("SELECT average(Warp) FROM shipslog")]
        [DataRow("SELECT count(*), DriveSpin FROM shipslog")]
        [DataRow("SELECT percentile(DriveSpin, 101) FROM shipslog")]
        [DataRow("SELECT rate(DriveSpin, 1 second) FROM shipslog")]
        [DataRow("SELECT derivative(DriveSpin, 0 seconds) FROM shipslog")]
        [DataRow("SELECT count(*) FROM shipslog FACET cases(DriveSpin < 1000) ")]
        [DataRow("SELECT count(*) FROM shipslog FACET buckets(DriveSpin, 0, 4)")]
        [DataRow("SELECT count(*) FROM shipslog GROUP BY DriveStage")]
        [DataRow("SELECT count(*) FROM shipslog SINCE yesterday")]
        [DataRow("SELECT DriveSpin FROM shipslog TIMESERIES 1 second")]
        [DataRow("SELECT avg(DriveSpin FROM shipslog")]
        public void ErrorsAreTheSame(string nrql)
        {
            // Act
            var query = Outcome(() => new TimeSeriesQuery<Sample>(nrql, Time).Evaluate(GetSamples()));
            var extension = Outcome(() => GetSamples().TimeSeriesQuery(nrql, Time));
            var parse = TimeSeriesSyntaxTree.Parse(nrql);
            var pipeline = Outcome(() => parse.Rewrite().Compile<Sample>().Execute(GetSamples(), Time));

            // Assert
            Assert.AreEqual(query, pipeline);
            Assert.AreEqual(extension, pipeline);
            Assert.StartsWith(nameof(SqlException) + ": " + (parse.Error?.Message ?? string.Empty), pipeline);
        }

        [TestMethod]
        [DataRow("SELECT max(twice(DriveSpin)) AS TopSpin FROM shipslog WHERE twice(DriveStage) > 2 FACET twice(DriveStage) LIMIT 1")]
        [DataRow("SELECT twice(DriveSpin) - twice(PlasmaPressure) AS D FROM shipslog LIMIT 2")]
        [DataRow("SELECT rate(sum(twice(DriveSpin)), 1 second) FROM shipslog FACET buckets(twice(PlasmaPressure), 200, 2)")]
        [DataRow("SELECT max(twice(DriveSpin, 2)) FROM shipslog")]
        public void RowsWithFunctionsAreTheSame(string nrql)
        {
            // Act
            var query = Outcome(() => new TimeSeriesQuery<Sample>(nrql, Time, null, Functions).Evaluate(GetSamples()));
            var pipeline = Outcome(() => TimeSeriesSyntaxTree.Parse(nrql, Functions).Compile<Sample>().Execute(GetSamples(), Time));

            // Assert
            Assert.AreEqual(query, pipeline);
        }

        [TestMethod]
        [DataRow("SELECT Id FROM shipslog LIMIT 2 -- top two", 2)]
        [DataRow("SELECT Id FROM shipslog /* FACET Quantum */ WHERE DriveStage > 1 -- SINCE 3\nLIMIT 3", 3)]
        [DataRow("SELECT Id /* , Note */ FROM shipslog WHERE Note <> '-- not a comment' AND DriveStage < 4", 6)]
        public void CommentsAreNotClauses(string nrql, int rows)
        {
            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(nrql);
            var result = syntax.Compile<Sample>().Execute(GetSamples(), Time);

            // Assert
            Assert.IsNull(syntax.Error);
            Assert.IsNull(syntax.Since);
            Assert.IsEmpty(syntax.Facets);
            Assert.HasCount(rows, result);
            Assert.IsTrue(result.All(r => r.Count == 2));
        }

        [TestMethod]
        [DataRow("SELECT count(*) FROM shipslog SINCE 99999999999999999999 seconds ago", "SINCE", "99999999999999999999 seconds ago")]
        [DataRow("SELECT count(*) FROM shipslog UNTIL 999999999999 hours", "UNTIL", "999999999999 hours")]
        [DataRow("SELECT count(*) FROM shipslog TIMESERIES 99999999999999 minutes", "TIMESERIES", "99999999999999 minutes")]
        [DataRow("SELECT count(*) FROM shipslog SESSION 99999999999999999999 h", "SESSION", "99999999999999999999 h")]
        [DataRow("SELECT count(*) FROM shipslog SINCE 1 COMPARE WITH 99999999999999999999 ms ago", "COMPARE WITH", "99999999999999999999 ms ago")]
        [DataRow("SELECT rate(count(*), 9999999999999999999999 seconds) FROM shipslog", "rate", "9999999999999999999999 seconds")]
        [DataRow("SELECT derivative(DriveSpin, 9999999999999999999999 h) FROM shipslog", "derivative", "9999999999999999999999 h")]
        public void HugeDurationsAreSqlExceptions(string nrql, string clause, string duration)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => new TimeSeriesQuery<Sample>(nrql, Time));

            // Assert
            Assert.StartsWith(clause + " " + duration + " ", e.Message);
            Assert.AreEqual(nrql.IndexOf(duration, StringComparison.Ordinal), e.Start);
            Assert.AreEqual(duration.Length, e.Length);
            Assert.AreEqual(e.Message, TimeSeriesSyntaxTree.Parse(nrql).Error.Message);
        }

        [TestMethod]
        public void RewriteLiftsTheAggregatesOutOfEachSelectItem()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT max(DriveSpin) - min(DriveSpin) AS Spread, rate(count(*), 1 second), Quantum FROM shipslog FACET Quantum");

            // Act
            var rewritten = syntax.Rewrite();

            // Assert
            Assert.AreEqual("max(DriveSpin) - min(DriveSpin)", syntax.Select[0].Expression.ToSql());
            Assert.AreEqual("agg0 - agg1", rewritten.Select[0].Expression.ToSql());
            Assert.AreEqual("max(DriveSpin), min(DriveSpin)", string.Join(", ", rewritten.Select[0].Aggregates.Select(a => a.ToSql())));
            Assert.AreEqual("agg0", rewritten.Select[1].Expression.ToSql());
            Assert.AreEqual("rate(count(*), 1 second)", rewritten.Select[1].Aggregates.Single().ToSql());
            Assert.AreEqual("Quantum", rewritten.Select[2].Expression.ToSql());
            Assert.IsEmpty(rewritten.Select[2].Aggregates);
            Assert.AreEqual(syntax.Text, rewritten.Text);
        }

        [TestMethod]
        public void RewritePassesEachCompiledExpressionThroughTheHook()
        {
            // Arrange
            var spin = SqlSyntaxTree.ParseExpression("DriveSpin * 0.001").Root;
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT max(Spin) AS Top, max(Spin) - min(Spin) AS Spread FROM shipslog WHERE Spin > 1 FACET Spin > 5 AS Fast, buckets(Spin, 8, 2) AS Band");

            // Act
            var rewritten = syntax.Rewrite(e => (ExpressionNode)e.Replace(n => n is NodeWithId id && id.Id.LookupId == "Spin" ? spin : null));
            var rows = rewritten.Compile<Sample>().Execute(GetSamples(), Time);

            // Assert
            Assert.AreEqual("DriveSpin * 0.001 > 1", rewritten.Where.ToSql());
            Assert.AreEqual("max(DriveSpin * 0.001)", rewritten.Select[0].Aggregates.Single().ToSql());
            Assert.AreEqual("DriveSpin * 0.001 > 5", rewritten.Facets[0].Expression.ToSql());
            Assert.AreEqual("buckets(DriveSpin * 0.001, 8, 2)", rewritten.Facets[1].Expression.ToSql());
            Assert.AreEqual("Spin > 1", syntax.Where.ToSql());
            Assert.AreEqual(7.9, rows.Max(r => (double)r["Top"]), 1e-9);
        }

        [TestMethod]
        public void HookThatReturnsNullKeepsTheExpression()
        {
            // Act
            var rows = TimeSeriesSyntaxTree.Parse("SELECT count(*) AS N FROM shipslog WHERE DriveStage > 2").Rewrite(e => null).Compile<Sample>().Execute(GetSamples(), Time);

            // Assert
            Assert.AreEqual(3, rows.Single()["N"]);
        }

        [TestMethod]
        public void TreeIsRewrittenOnce()
        {
            // Arrange
            var rewritten = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog").Rewrite();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => rewritten.Rewrite());
        }

        [TestMethod]
        public void ParseFindsTheTextErrorsAndCompileTheNames()
        {
            // Arrange
            const string Nrql = "SELECT max(Warp) FROM shipslog SINCE soon";

            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(Nrql);
            var compile = Assert.ThrowsExactly<SqlException>(() => syntax.Compile<Sample>());
            var query = Assert.ThrowsExactly<SqlException>(() => new TimeSeriesQuery<Sample>(Nrql, Time));

            // Assert
            Assert.AreEqual("soon", Nrql.Substring(syntax.Error.Start, syntax.Error.Length));
            Assert.AreEqual("Warp", Nrql.Substring(compile.Start, compile.Length));
            Assert.AreEqual(query.Message, compile.Message);
        }

        [TestMethod]
        public void OnePlanRunsOverEachSourceAndClock()
        {
            // Arrange
            var plan = TimeSeriesSyntaxTree.Parse("SELECT count(*) AS N FROM shipslog TIMESERIES 1 second").Compile<Sample>();

            // Act
            var seconds = plan.Execute(GetSamples(), Time);
            var halves = plan.Execute(GetSamples().Take(4), s => TimeSpan.FromSeconds(s.Time / 2));

            // Assert
            Assert.AreEqual("BeginTime, EndTime, N", string.Join(", ", plan.Columns));
            Assert.AreEqual("2, 2, 2, 2", string.Join(", ", seconds.Select(r => r["N"])));
            Assert.AreEqual("4", string.Join(", ", halves.Select(r => r["N"])));
            Assert.ThrowsExactly<ArgumentNullException>(() => plan.Execute(GetSamples(), null));
        }

        [TestMethod]
        public void QueryRunsItsPlan()
        {
            // Arrange
            var query = new TimeSeriesQuery<Sample>("SELECT max(DriveSpin) AS Top FROM shipslog FACET Quantum", Time);

            // Act
            var rows = query.Evaluate(GetSamples());

            // Assert
            Assert.AreEqual("Quantum, Top", string.Join(", ", query.Plan.Columns));
            Assert.AreEqual(Outcome(() => query.Plan.Execute(GetSamples(), Time)), Outcome(() => rows));
            Assert.AreEqual("SELECT max(DriveSpin) AS Top FROM shipslog FACET Quantum", query.Plan.Syntax.Text);
        }
    }
}
