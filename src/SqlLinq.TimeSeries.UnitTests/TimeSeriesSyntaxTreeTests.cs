using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries.UnitTests
{
    [TestClass]
    public class TimeSeriesSyntaxTreeTests
    {
        private const string OneLine = "SELECT count(*) AS N FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 1 TIMESERIES 1 second";
        private const string Lines = "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 1\nTIMESERIES 1 second";
        private const string Indented = "SELECT count(*) AS N\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET Quantum\n  SINCE 1\n  TIMESERIES 1 second\n";
        private const string Commented = "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET Quantum\nSINCE 1 -- warm\nTIMESERIES 1 second";
        private const string Crlf = "SELECT count(*) AS N\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET Quantum\r\nSINCE 1\r\nTIMESERIES 1 second";

        private static readonly string[] FacetTexts = ["Quantum", "buckets(PlasmaPressure, 120, 3) AS P", "cases(DriveStage < 2 AS 'low', DriveStage >= 2 AS 'high')"];

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

        private static string Run(TimeSeriesSyntaxTree syntax)
        {
            var rows = syntax.Compile<Sample>().Execute(GetSamples(), Time);

            return string.Join("; ", rows.Select(r => string.Join(", ", r.Select(c => c.Key + "=" + Convert.ToString(c.Value, CultureInfo.InvariantCulture)))));
        }

        private static string Describe(TimeSeriesSyntaxTree syntax)
        {
            return string.Join(
                " | ",
                string.Join(", ", syntax.Clauses.Select(c => c.Keyword).OrderBy(k => k, StringComparer.Ordinal)),
                string.Join(", ", syntax.Select.Concat(syntax.Facets).Select(i => i.Text + "/" + i.Alias + "/" + string.Join(",", i.Aggregates.Select(a => a.FunctionName)) + "/" + string.Join(",", i.Columns))),
                syntax.Where?.ToSql(),
                string.Join(", ", syntax.Names.Select(n => n.LookupId).OrderBy(n => n, StringComparer.Ordinal)),
                syntax.Since?.Offset + "/" + syntax.Since?.Ago + "/" + syntax.Until?.Offset + "/" + syntax.TimeSeries + "/" + syntax.Session + "/" + syntax.Compare + "/" + syntax.Limit,
                syntax.Error?.Message);
        }

        private static string Positions(TimeSeriesSyntaxTree syntax)
        {
            var items = syntax.Select.Concat(syntax.Facets).ToList();
            var expressions = items.Select(i => i.Expression).Append(syntax.Where).Where(e => e != null);

            return string.Join(
                " | ",
                string.Join(", ", syntax.Clauses.Select(c => c.Keyword + ":" + c.Text + "@" + c.Start + "/" + c.KeywordStart + "/" + c.Length)),
                string.Join(", ", items.Select(i => i.Text + "@" + i.Start)),
                string.Join(", ", syntax.Names.Select(n => n.LookupId + "@" + n.Start + "/" + n.Length)),
                string.Join(", ", syntax.Tokens.Select(t => t.Kind + ":" + t.Text + "@" + t.Start)),
                string.Join(", ", expressions.SelectMany(Terminals).Select(t => t.Text + "@" + t.Start + "/" + t.LineNumber + ":" + t.LinePosition)),
                syntax.Error?.Kind + "@" + syntax.Error?.Start + "/" + syntax.Error?.Length);
        }

        private static IEnumerable<TerminalNode> Terminals(SyntaxNode node)
        {
            if (node is TerminalNode terminal)
            {
                return [terminal];
            }

            var parent = (NonTerminalNode)node;
            var arguments = (parent as NodeWithId)?.Arguments ?? [];

            return parent.Children.SelectMany(Terminals).Concat(arguments.Where(a => a.Expression != null).SelectMany(a => Terminals(a.Expression)));
        }

        private static TimeSeriesSyntaxTree Edited(TimeSeriesSyntaxTree syntax, string edits)
        {
            return edits.Split(';').Aggregate(syntax, (tree, edit) =>
                edit.EndsWith('-') ? tree.Replace(edit.TrimEnd('-'), null)
                : edit.Contains('+') ? tree.Add(edit.Split('+')[0], edit.Split('+')[1])
                : tree.Replace(edit.Split('=')[0], edit.Split('=')[1]));
        }

        [TestMethod]
        [DataRow("SELECT count(*) AS N FROM shipslog SINCE 1", "Replace", "SINCE", "2 seconds ago", "SELECT count(*) AS N FROM shipslog SINCE 2 seconds ago")]
        [DataRow("SELECT count(*) AS N FROM shipslog UNTIL 3", "Replace", "until", " 2 ", "SELECT count(*) AS N FROM shipslog UNTIL 2")]
        [DataRow("SELECT Id FROM shipslog LIMIT 2", "Replace", "LIMIT", "3", "SELECT Id FROM shipslog LIMIT 3")]
        [DataRow("SELECT count(*) AS N FROM shipslog", "Replace", "TIMESERIES", "1 second", "SELECT count(*) AS N FROM shipslog TIMESERIES 1 second")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET Quantum", "Replace", "SESSION", "500 ms", "SELECT count(*) AS N FROM shipslog FACET Quantum SESSION 500 ms")]
        [DataRow("SELECT count(*) AS N FROM shipslog SINCE 2", "Replace", "COMPARE", "WITH 1 second ago", "SELECT count(*) AS N FROM shipslog SINCE 2 COMPARE WITH 1 second ago")]
        [DataRow("SELECT count(*) AS N FROM shipslog WHERE DriveStage > 1", "Replace", "WHERE", "Quantum OR Note = 'Kaylon'", "SELECT count(*) AS N FROM shipslog WHERE Quantum OR Note = 'Kaylon'")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET Quantum LIMIT 1", "Replace", "FACET", "DriveStage, buckets(PlasmaPressure, 120, 3) AS P", "SELECT count(*) AS N FROM shipslog FACET DriveStage, buckets(PlasmaPressure, 120, 3) AS P LIMIT 1")]
        [DataRow("SELECT count(*) FROM shipslog FACET Quantum", "Replace", "SELECT", "max(DriveSpin) AS Top, Quantum", "SELECT max(DriveSpin) AS Top, Quantum FROM shipslog FACET Quantum")]
        [DataRow("SELECT Id FROM shipslog", "Replace", "FROM", "[ships log]", "SELECT Id FROM [ships log]")]
        [DataRow("SELECT count(*) AS N FROM shipslog WHERE Quantum TIMESERIES 1 second", "Replace", "WHERE", null, "SELECT count(*) AS N FROM shipslog TIMESERIES 1 second")]
        [DataRow("SELECT count(*) AS N FROM shipslog SINCE 2 TIMESERIES 1 second", "Replace", "TIMESERIES", null, "SELECT count(*) AS N FROM shipslog SINCE 2")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET Quantum", "Replace", "FACET", null, "SELECT count(*) AS N FROM shipslog")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET Quantum", "Add", "SELECT", "max(DriveSpin) AS Top, percentile(PlasmaPressure, 5, 95)", "SELECT count(*) AS N, max(DriveSpin) AS Top, percentile(PlasmaPressure, 5, 95) FROM shipslog FACET Quantum")]
        [DataRow("SELECT Id FROM shipslog LIMIT 2", "Add", "select", "DriveSpin * 2 AS Twice", "SELECT Id, DriveSpin * 2 AS Twice FROM shipslog LIMIT 2")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET Quantum", "Add", "FACET", "DriveStage", "SELECT count(*) AS N FROM shipslog FACET Quantum, DriveStage")]
        [DataRow("SELECT count(*) AS N FROM shipslog TIMESERIES 1 second", "Add", "FACET", "cases(DriveStage < 2 AS 'low', DriveStage >= 2 AS 'high')", "SELECT count(*) AS N FROM shipslog FACET cases(DriveStage < 2 AS 'low', DriveStage >= 2 AS 'high') TIMESERIES 1 second")]
        public void EditedTreeIsTheTreeOfTheEditedText(string nrql, string edit, string keyword, string text, string equivalent)
        {
            // Arrange
            var original = TimeSeriesSyntaxTree.Parse(nrql);
            var parsed = TimeSeriesSyntaxTree.Parse(equivalent);

            // Act
            var edited = edit == "Add" ? original.Add(keyword, text) : original.Replace(keyword, text);

            // Assert
            Assert.IsNull(edited.Error);
            Assert.AreEqual(Describe(parsed), Describe(edited));
            Assert.AreEqual(Run(parsed), Run(edited));
            Assert.AreEqual(parsed.ToSql(), edited.ToSql());
            Assert.AreEqual(Describe(TimeSeriesSyntaxTree.Parse(nrql)), Describe(original));
            Assert.AreEqual(equivalent, edited.Text);
        }

        [TestMethod]
        public void EditReusesTheUnchangedPartsAtTheirNewPositions()
        {
            // Arrange
            const string Nrql = "SELECT max(DriveSpin) AS Top FROM shipslog WHERE DriveStage > 1 FACET Quantum SINCE 1 second ago";
            var original = TimeSeriesSyntaxTree.Parse(Nrql);
            var shift = ", average(FuelMix)".Length;

            // Act
            var edited = original.Add("SELECT", "average(FuelMix)").Replace("SINCE", "  2 seconds ago");

            // Assert
            Assert.AreEqual("SELECT max(DriveSpin) AS Top, average(FuelMix) FROM shipslog WHERE DriveStage > 1 FACET Quantum SINCE 2 seconds ago", edited.Text);
            Assert.AreSame(original.Select[0].Expression, edited.Select[0].Expression);
            Assert.AreNotSame(original.Where, edited.Where);
            Assert.AreEqual(original.Where.ToSql() + "@" + (original.Where.Start + shift), edited.Where.ToSql() + "@" + edited.Where.Start);
            Assert.AreNotSame(original.Facets[0], edited.Facets[0]);
            Assert.AreEqual(original.Facets[0].Text + "@" + (original.Facets[0].Start + shift), edited.Facets[0].Text + "@" + edited.Facets[0].Start);
            Assert.AreEqual(Positions(TimeSeriesSyntaxTree.Parse(edited.Text)), Positions(edited));
            Assert.AreEqual(TimeSpan.FromSeconds(2), edited.Since.Offset);
            Assert.AreEqual(TimeSpan.FromSeconds(1), original.Since.Offset);
            Assert.AreEqual(Nrql, original.Text);
            Assert.HasCount(1, original.Select);
        }

        [TestMethod]
        [DataRow("SELECT count(*) FROM shipslog SINCE 1 hou", "SINCE=1 hour ago", null)]
        [DataRow("SELECT count(*) FROM shipslog SINCE 1", "SINCE= 1 hou", "1 hou")]
        [DataRow("SELECT count(*) FROM shipslog", "SELECT+ Id", "Id")]
        [DataRow("SELECT count(*) FROM shipslog WHERE DriveSpin > SINCE 1", "SINCE=2", "")]
        [DataRow("SELECT count(*) FROM shipslog SINCE 2 COMPARE WITH 1 second ago", "SINCE-", "WITH 1 second ago")]
        [DataRow("SELECT count(*) FROM shipslog SINCE 1 /* open", "SINCE=2 seconds ago", "/* open")]
        public void EditErrorsArePositionedInTheEditedText(string nrql, string edit, string text)
        {
            // Act
            var edited = Edited(TimeSeriesSyntaxTree.Parse(nrql), edit);
            var reparsed = TimeSeriesSyntaxTree.Parse(edited.ToSql()).Error;

            // Assert
            Assert.AreEqual(text, edited.Error == null ? null : edited.ToSql().Substring(edited.Error.Start, edited.Error.Length));
            Assert.AreEqual(reparsed?.Message + "@" + reparsed?.Start + "/" + reparsed?.Length + "/" + reparsed?.Kind, edited.Error?.Message + "@" + edited.Error?.Start + "/" + edited.Error?.Length + "/" + edited.Error?.Kind);
        }

        [TestMethod]
        public void EditTakesOnlyClausesOfTheDialect()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog SINCE 1");
            var rewritten = syntax.Rewrite();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Replace("GROUP", "Id"));
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Replace("SELECT", null));
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Replace("FROM", null));
            Assert.ThrowsExactly<ArgumentException>(() => syntax.Add("SINCE", "2"));
            Assert.ThrowsExactly<ArgumentNullException>(() => syntax.Add("SELECT", null));
            Assert.ThrowsExactly<ArgumentNullException>(() => syntax.Replace(null, "2"));
            Assert.ThrowsExactly<InvalidOperationException>(() => rewritten.Replace("SINCE", "2"));
            Assert.ThrowsExactly<InvalidOperationException>(() => rewritten.Add("SELECT", "max(Id)"));
        }

        [TestMethod]
        [DataRow("select  count(*) as N from shipslog\n  since   2\tuntil 4 ", "SELECT count(*) as N FROM shipslog SINCE 2 UNTIL 4")]
        [DataRow("SELECT max(DriveSpin) /* top */ AS Top FROM shipslog LIMIT 2 FACET Quantum -- by stage\nSINCE 1 second ago", "SELECT max(DriveSpin) AS Top FROM shipslog FACET Quantum SINCE 1 second ago LIMIT 2")]
        [DataRow("SELECT Id FROM shipslog WHERE Note <> 'a  b,  c' AND DriveStage>1", "SELECT Id FROM shipslog WHERE Note <> 'a  b,  c' AND DriveStage>1")]
        [DataRow("SELECT count(*) FROM [ships log] COMPARE with 1 second ago SINCE 2 TIMESERIES 1 second", "SELECT count(*) FROM [ships log] SINCE 2 TIMESERIES 1 second COMPARE with 1 second ago")]
        public void NormalizedToSqlWritesTheQueryBack(string nrql, string expected)
        {
            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(nrql);
            var written = syntax.ToSql(normalize: true);

            // Assert
            Assert.AreEqual(expected, written);
            Assert.AreEqual(written, TimeSeriesSyntaxTree.Parse(written).ToSql(normalize: true));
            Assert.AreEqual(nrql, syntax.ToSql(normalize: false));
        }

        [TestMethod]
        [DataRow("SELECT max(DriveSpin) AS Top, count(*) FROM shipslog WHERE DriveStage > 1 FACET Quantum TIMESERIES 1 second")]
        [DataRow("SELECT max(DriveSpin) - min(DriveSpin) AS Spread FROM shipslog FACET buckets(PlasmaPressure, 120, 3) SESSION 600 ms")]
        [DataRow("SELECT count(*) AS N, latest(Note) FROM shipslog SINCE 1 COMPARE WITH 1 second ago")]
        [DataRow("SELECT Id, DriveSpin * 2 AS Twice FROM shipslog WHERE Quantum LIMIT 2")]
        [DataRow("SELECT percentile(DriveSpin, 5, 95) AS P, uniqueCount(DriveStage) FROM shipslog -- all\nFACET cases(FuelMix < 13 AS 'rich', FuelMix >= 13 AS 'lean') AS Mix UNTIL 3")]
        public void ToSqlParsesToTheSameQuery(string nrql)
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse(nrql);

            // Act
            var reparsed = TimeSeriesSyntaxTree.Parse(syntax.ToSql(normalize: true));

            // Assert
            Assert.AreEqual(Describe(syntax), Describe(reparsed));
            Assert.AreEqual(Run(syntax), Run(reparsed));
            Assert.AreEqual(nrql, syntax.ToSql());
        }

        [TestMethod]
        public void ToSqlWritesTheEditAndARewrittenTreeItsQuery()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT count(*) AS N FROM shipslog WHERE DriveSpin > 1000 SINCE 1");

            // Act
            var edited = syntax.Add("SELECT", "max(DriveSpin)").Add("FACET", "Quantum").Replace("SINCE", null).Replace("WHERE", "DriveStage   > 0");
            var rewritten = syntax.Rewrite(e => (SyntaxTree.Expressions.ExpressionNode)SqlSyntaxTree.ParseExpression("Id > 0").Root);

            // Assert
            Assert.AreEqual("SELECT count(*) AS N, max(DriveSpin) FROM shipslog WHERE DriveStage   > 0 FACET Quantum", edited.ToSql());
            Assert.AreEqual("SELECT count(*) AS N, max(DriveSpin) FROM shipslog WHERE DriveStage > 0 FACET Quantum", edited.ToSql(normalize: true));
            Assert.AreEqual(syntax.Text, rewritten.ToSql());
            Assert.AreEqual(syntax.ToSql(normalize: true), rewritten.ToSql(normalize: true));
            Assert.AreEqual(syntax.Text, syntax.Compile<Sample>().Syntax.ToSql());
        }

        [TestMethod]
        [DataRow(OneLine, "SINCE=2 seconds ago", "SELECT count(*) AS N FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 2 seconds ago TIMESERIES 1 second")]
        [DataRow(OneLine, "TIMESERIES-", "SELECT count(*) AS N FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 1")]
        [DataRow(OneLine, "WHERE-", "SELECT count(*) AS N FROM shipslog FACET Quantum SINCE 1 TIMESERIES 1 second")]
        [DataRow(OneLine, "LIMIT=2", "SELECT count(*) AS N FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 1 TIMESERIES 1 second LIMIT 2")]
        [DataRow(OneLine, "UNTIL=3", "SELECT count(*) AS N FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 1 UNTIL 3 TIMESERIES 1 second")]
        [DataRow(OneLine, "SELECT+max(DriveSpin) AS Top", "SELECT count(*) AS N, max(DriveSpin) AS Top FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 1 TIMESERIES 1 second")]
        [DataRow(OneLine, "FACET=DriveStage;TIMESERIES-", "SELECT count(*) AS N FROM shipslog WHERE DriveStage > 0 FACET DriveStage SINCE 1")]
        [DataRow(Lines, "SINCE=2 seconds ago", "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 2 seconds ago\nTIMESERIES 1 second")]
        [DataRow(Lines, "TIMESERIES-", "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 1")]
        [DataRow(Lines, "WHERE-", "SELECT count(*) AS N\nFROM shipslog\nFACET Quantum\nSINCE 1\nTIMESERIES 1 second")]
        [DataRow(Lines, "LIMIT=2", "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 1\nTIMESERIES 1 second\nLIMIT 2")]
        [DataRow(Lines, "UNTIL=3", "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 1\nUNTIL 3\nTIMESERIES 1 second")]
        [DataRow(Lines, "SELECT+max(DriveSpin) AS Top", "SELECT count(*) AS N, max(DriveSpin) AS Top\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 1\nTIMESERIES 1 second")]
        [DataRow(Lines, "FACET=DriveStage;TIMESERIES-", "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET DriveStage\nSINCE 1")]
        [DataRow(Indented, "SINCE=2 seconds ago", "SELECT count(*) AS N\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET Quantum\n  SINCE 2 seconds ago\n  TIMESERIES 1 second\n")]
        [DataRow(Indented, "TIMESERIES-", "SELECT count(*) AS N\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET Quantum\n  SINCE 1\n")]
        [DataRow(Indented, "WHERE-", "SELECT count(*) AS N\n  FROM shipslog\n  FACET Quantum\n  SINCE 1\n  TIMESERIES 1 second\n")]
        [DataRow(Indented, "LIMIT=2", "SELECT count(*) AS N\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET Quantum\n  SINCE 1\n  TIMESERIES 1 second\n  LIMIT 2\n")]
        [DataRow(Indented, "UNTIL=3", "SELECT count(*) AS N\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET Quantum\n  SINCE 1\n  UNTIL 3\n  TIMESERIES 1 second\n")]
        [DataRow(Indented, "SELECT+max(DriveSpin) AS Top", "SELECT count(*) AS N, max(DriveSpin) AS Top\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET Quantum\n  SINCE 1\n  TIMESERIES 1 second\n")]
        [DataRow(Indented, "FACET=DriveStage;TIMESERIES-", "SELECT count(*) AS N\n  FROM shipslog\n  WHERE DriveStage > 0\n  FACET DriveStage\n  SINCE 1\n")]
        [DataRow(Commented, "SINCE=2 seconds ago", "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET Quantum\nSINCE 2 seconds ago -- warm\nTIMESERIES 1 second")]
        [DataRow(Commented, "TIMESERIES-", "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET Quantum\nSINCE 1 -- warm")]
        [DataRow(Commented, "WHERE-", "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\n/* moving */\nFACET Quantum\nSINCE 1 -- warm\nTIMESERIES 1 second")]
        [DataRow(Commented, "LIMIT=2", "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET Quantum\nSINCE 1 -- warm\nTIMESERIES 1 second\nLIMIT 2")]
        [DataRow(Commented, "UNTIL=3", "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET Quantum\nSINCE 1 -- warm\nUNTIL 3\nTIMESERIES 1 second")]
        [DataRow(Commented, "SELECT+max(DriveSpin) AS Top", "-- drive load\nSELECT count(*) AS N, max(DriveSpin) AS Top -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET Quantum\nSINCE 1 -- warm\nTIMESERIES 1 second")]
        [DataRow(Commented, "FACET=DriveStage;TIMESERIES-", "-- drive load\nSELECT count(*) AS N -- samples\nFROM shipslog\nWHERE DriveStage > 0 /* moving */\nFACET DriveStage\nSINCE 1 -- warm")]
        [DataRow(Crlf, "SINCE=2 seconds ago", "SELECT count(*) AS N\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET Quantum\r\nSINCE 2 seconds ago\r\nTIMESERIES 1 second")]
        [DataRow(Crlf, "TIMESERIES-", "SELECT count(*) AS N\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET Quantum\r\nSINCE 1")]
        [DataRow(Crlf, "WHERE-", "SELECT count(*) AS N\r\nFROM shipslog\r\nFACET Quantum\r\nSINCE 1\r\nTIMESERIES 1 second")]
        [DataRow(Crlf, "LIMIT=2", "SELECT count(*) AS N\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET Quantum\r\nSINCE 1\r\nTIMESERIES 1 second\r\nLIMIT 2")]
        [DataRow(Crlf, "UNTIL=3", "SELECT count(*) AS N\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET Quantum\r\nSINCE 1\r\nUNTIL 3\r\nTIMESERIES 1 second")]
        [DataRow(Crlf, "SELECT+max(DriveSpin) AS Top", "SELECT count(*) AS N, max(DriveSpin) AS Top\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET Quantum\r\nSINCE 1\r\nTIMESERIES 1 second")]
        [DataRow(Crlf, "FACET=DriveStage;TIMESERIES-", "SELECT count(*) AS N\r\nFROM shipslog\r\nWHERE DriveStage > 0\r\nFACET DriveStage\r\nSINCE 1")]
        [DataRow("SELECT count(*) AS N FROM shipslog\nWHERE DriveStage /* stage */ > 0 -- moving\nSINCE 1", "WHERE=Quantum", "SELECT count(*) AS N FROM shipslog\nWHERE Quantum -- moving\nSINCE 1")]
        [DataRow("SELECT count(*) AS N,\n       max(DriveSpin) AS Top\nFROM shipslog\nFACET Quantum", "SELECT+min(DriveSpin) AS Low;SELECT+average(FuelMix)", "SELECT count(*) AS N,\n       max(DriveSpin) AS Top,\n       min(DriveSpin) AS Low,\n       average(FuelMix)\nFROM shipslog\nFACET Quantum")]
        [DataRow("SELECT count(*) AS N\r\nFROM shipslog SINCE 1\r\nTIMESERIES 1 second", "SINCE-", "SELECT count(*) AS N\r\nFROM shipslog\r\nTIMESERIES 1 second")]
        [DataRow(Lines, "SINCE=2;SINCE-;SINCE=3", "SELECT count(*) AS N\nFROM shipslog\nWHERE DriveStage > 0\nFACET Quantum\nSINCE 3\nTIMESERIES 1 second")]
        [DataRow("SELECT count(*) AS N FROM shipslog FACET Quantum,DriveStage -- top", "FACET+Id;LIMIT=2", "SELECT count(*) AS N FROM shipslog FACET Quantum,DriveStage,Id LIMIT 2 -- top")]
        [DataRow("SELECT count(*) AS N FROM shipslog LIMIT 2 FACET Quantum", "SINCE=1;WHERE=Quantum", "SELECT count(*) AS N FROM shipslog WHERE Quantum LIMIT 2 FACET Quantum SINCE 1")]
        public void ToSqlKeepsTheTextAroundAnEdit(string nrql, string edits, string expected)
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse(nrql);

            // Act
            var edited = edits.Split(';').Aggregate(syntax, (tree, edit) =>
                edit.EndsWith('-') ? tree.Replace(edit.TrimEnd('-'), null)
                : edit.Contains('+') ? tree.Add(edit.Split('+')[0], edit.Split('+')[1])
                : tree.Replace(edit.Split('=')[0], edit.Split('=')[1]));
            var printed = edited.ToSql();
            var reparsed = TimeSeriesSyntaxTree.Parse(printed);

            // Assert
            Assert.AreEqual(expected, printed);
            Assert.IsNull(edited.Error);
            Assert.AreEqual(Describe(edited), Describe(reparsed));
            Assert.AreEqual(Run(edited), Run(reparsed));
            Assert.AreEqual(nrql, syntax.ToSql());
        }

        [TestMethod]
        [DataRow(OneLine)]
        [DataRow(Lines)]
        [DataRow(Indented)]
        [DataRow(Commented)]
        [DataRow(Crlf)]
        [DataRow("  select max(DriveSpin) /* top */ AS Top, count(*)\r\n\tfrom [ships log] -- all\n LIMIT 2 FACET Quantum GROUP BY Id SINCE 1 SINCE 2 /* open")]
        public void ToSqlOfAnUneditedTreeIsItsText(string nrql)
        {
            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(nrql, Functions);

            // Assert
            Assert.AreEqual(nrql, syntax.ToSql());
            Assert.AreEqual(nrql, syntax.Bind(Functions).ToSql());
            Assert.AreEqual(nrql, syntax.Rewrite().ToSql());
        }

        [TestMethod]
        public void BindSuppliesTheFunctionsAQueryWasParsedWithout()
        {
            // Arrange
            const string Nrql = "SELECT max(twice(DriveSpin)) AS M, count(*) AS N FROM shipslog WHERE twice(DriveStage) > 2 FACET buckets(twice(PlasmaPressure), 240, 2) AS P";
            var standIn = new ISqlFunction[] { new TestFunction("twice", arguments => throw new SqlException("twice isn't bound.")) };
            var aggregate = new TestFunction("max", arguments => throw new SqlException("max isn't the aggregate."));
            var syntax = TimeSeriesSyntaxTree.Parse(Nrql, standIn);
            var expected = new TimeSeriesQuery<Sample>(Nrql, Time, null, Functions).Evaluate(GetSamples());

            // Act
            var bound = syntax.Bind(Functions.Append(aggregate));
            var edited = bound.Replace("WHERE", "twice(DriveStage) > 4");

            // Assert
            Assert.IsNull(syntax.Error);
            Assert.AreEqual(Describe(syntax), Describe(bound));
            Assert.AreEqual(Run(TimeSeriesSyntaxTree.Parse(Nrql, Functions)), Run(bound));
            Assert.AreEqual(Run(TimeSeriesSyntaxTree.Parse(Nrql, Functions).Rewrite()), Run(syntax.Rewrite().Bind(Functions)));
            Assert.AreEqual(Run(TimeSeriesSyntaxTree.Parse(Nrql, Functions).Replace("WHERE", "twice(DriveStage) > 4")), Run(edited));
            Assert.HasCount(expected.Count, bound.Compile<Sample>().Execute(GetSamples(), Time));
            Assert.AreEqual("twice isn't bound.", Assert.ThrowsExactly<SqlException>(() => syntax.Compile<Sample>()).Message);
            Assert.ThrowsExactly<ArgumentNullException>(() => syntax.Bind(null));
        }

        [TestMethod]
        public void BindReadsAQueryParsedWithoutItsFunctions()
        {
            // Arrange
            const string Nrql = "SELECT max(twice(DriveSpin)) AS M, count(*) AS N FROM shipslog WHERE twice(DriveStage) > 2 FACET twice(DriveStage)";
            var parsed = TimeSeriesSyntaxTree.Parse(Nrql, Functions);

            // Act
            var bound = TimeSeriesSyntaxTree.Parse(Nrql).Bind(Functions);

            // Assert
            Assert.IsNull(bound.Error);
            Assert.AreEqual(Describe(parsed), Describe(bound));
            Assert.AreEqual(Run(parsed), Run(bound));
        }

        [TestMethod]
        public void ParsedFacetCompilesAndLabelsAsItsText()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog FACET " + string.Join(", ", FacetTexts));
            var samples = GetSamples();

            // Act
            var parsed = syntax.Facets.Select(f => FacetExpression.Compile<Sample>(f)).Select(f => string.Join(",", samples.Select(f))).ToList();
            var texts = FacetTexts.Select(t => FacetExpression.Compile<Sample>(t)).Select(f => string.Join(",", samples.Select(f))).ToList();

            // Assert
            CollectionAssert.AreEqual(texts, parsed);
            CollectionAssert.AreEqual(FacetTexts.Select(t => FacetExpression.Labels(t) == null ? null : string.Join(",", FacetExpression.Labels(t))).ToList(), syntax.Facets.Select(f => FacetExpression.Labels(f) == null ? null : string.Join(",", FacetExpression.Labels(f))).ToList());
            Assert.AreEqual("< 40,40 - < 80,>= 80", string.Join(",", FacetExpression.Labels(syntax.Facets[1])));
        }

        [TestMethod]
        public void ParsedFacetMustBeAWorkingFacet()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog FACET buckets(PlasmaPressure, 0, 3)");

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => FacetExpression.Compile<Sample>(syntax.Facets[0]));

            // Assert
            Assert.AreEqual(Assert.ThrowsExactly<SqlException>(() => FacetExpression.Compile<Sample>("buckets(PlasmaPressure, 0, 3)")).Message, e.Message);
            Assert.ThrowsExactly<SqlException>(() => FacetExpression.Labels(syntax.Facets[0]));
            Assert.ThrowsExactly<ArgumentException>(() => FacetExpression.Compile<Sample>(syntax.Select[0]));
            Assert.ThrowsExactly<ArgumentException>(() => FacetExpression.Labels(syntax.Select[0]));
            Assert.ThrowsExactly<ArgumentNullException>(() => FacetExpression.Labels((TimeSeriesExpression)null));
        }

        [TestMethod]
        public void ItemColumnsAreTheResultColumnsEachItemFills()
        {
            // Act
            var aggregated = TimeSeriesSyntaxTree.Parse("SELECT DriveStage, percentile(DriveSpin, 5, 95) AS P, percentile(FuelMix, 50, 90), max(DriveSpin) - min(DriveSpin) AS Spread, count(*) FROM shipslog FACET DriveStage, Quantum AS Q");
            var plain = TimeSeriesSyntaxTree.Parse("SELECT Id, DriveSpin * 2 AS Twice FROM shipslog");

            // Assert
            Assert.AreEqual(" | P.5,P.95 | percentile(FuelMix, 50),percentile(FuelMix, 90) | Spread | count(*)", string.Join(" | ", aggregated.Select.Select(i => string.Join(",", i.Columns))));
            Assert.AreEqual("DriveStage | Q", string.Join(" | ", aggregated.Facets.Select(i => string.Join(",", i.Columns))));
            Assert.AreEqual("Id | Twice", string.Join(" | ", plain.Select.Select(i => string.Join(",", i.Columns))));
        }

        [TestMethod]
        [DataRow("SELECT Id, DriveSpin * 2 AS Twice FROM shipslog SINCE 2 COMPARE WITH 2 seconds", "Comparison:Comparison:, Time:Time:, Id:Select:Id, Twice:Select:DriveSpin * 2")]
        [DataRow("SELECT DriveStage, percentile(DriveSpin, 5, 95) AS P, count(*) FROM shipslog FACET DriveStage, Quantum TIMESERIES 1 second", "BeginTime:Time:, EndTime:Time:, DriveStage:Facet:DriveStage, Quantum:Facet:Quantum, P.5:Select:percentile(DriveSpin, 5, 95), P.95:Select:percentile(DriveSpin, 5, 95), count(*):Select:count(*)")]
        [DataRow("SELECT DriveSpin AS time FROM shipslog", "Time:Select:DriveSpin")]
        [DataRow("SELECT count(*) AS Quantum, max(Id) AS begintime FROM shipslog FACET Quantum SESSION 1 second", "BeginTime:Select:max(Id), EndTime:Time:, Quantum:Select:count(*)")]
        public void ColumnDescriptionsSayWhatFillsEachColumn(string nrql, string expected)
        {
            // Arrange
            var plan = TimeSeriesSyntaxTree.Parse(nrql).Compile<Sample>();
            var items = plan.Syntax.Select.Concat(plan.Syntax.Facets).ToList();

            // Act
            var columns = plan.ColumnDescriptions;

            // Assert
            Assert.AreEqual(expected, string.Join(", ", columns.Select(c => c.Name + ":" + c.Kind + ":" + c.Item?.Text)));
            CollectionAssert.AreEqual(plan.Columns.ToList(), columns.Select(c => c.Name).ToList());
            Assert.IsTrue(columns.All(c => c.Item == null ? c.Kind == TimeSeriesColumnKind.Time || c.Kind == TimeSeriesColumnKind.Comparison : items.Contains(c.Item)));
            CollectionAssert.AreEqual(plan.Columns.ToList(), plan.Execute(GetSamples(), Time)[0].Keys.ToList());
        }

        [TestMethod]
        [DataRow("SELECT max(DriveSpin) AS Top FROM shipslog WHERE DriveStage > 0 FACET Quantum SINCE 1 second ago COMPARE WITH 2 seconds ago")]
        [DataRow("  select count(*) as N\n from  [ships log]\n  since   2\tuntil 4 ")]
        [DataRow("SELECT Id FROM shipslog WHERE Note <> 'since limit' LIMIT 2")]
        public void ClausesSpanFromTheirKeyword(string nrql)
        {
            // Act
            var clauses = TimeSeriesSyntaxTree.Parse(nrql).Clauses;

            // Assert
            foreach (var clause in clauses)
            {
                var span = nrql.Substring(clause.KeywordStart, clause.Length);
                Assert.StartsWith(clause.Keyword, span.ToUpperInvariant(), clause.Keyword);
                Assert.EndsWith(clause.Text, span, clause.Keyword);
                Assert.AreEqual(clause.Start + clause.Text.Length, clause.KeywordStart + clause.Length, clause.Keyword);
            }
        }

        [TestMethod]
        [DataRow("30 seconds", 30000.0, false)]
        [DataRow("  5 minutes ago ", 300000.0, true)]
        [DataRow("500 ms", 500.0, false)]
        [DataRow("1.5 h", 5400000.0, false)]
        public void BoundTryParseReadsADuration(string text, double milliseconds, bool ago)
        {
            // Act
            var read = Bound.TryParse(text, "SINCE", out var bound, out var error);

            // Assert
            Assert.IsTrue(read);
            Assert.IsNull(error);
            Assert.AreEqual(TimeSpan.FromMilliseconds(milliseconds), bound.Offset);
            Assert.AreEqual(ago, bound.Ago);
            Assert.AreEqual(TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog SINCE " + text).Since.Offset, bound.Offset);
        }

        [TestMethod]
        [DataRow("5 mintues", "SINCE")]
        [DataRow(" 2 seconds later", "UNTIL")]
        [DataRow("", "TIMESERIES")]
        [DataRow("99999999999999 hours", "SESSION")]
        [DataRow("5 minutes ago", "TIMESERIES")]
        [DataRow(" 0 seconds", "SESSION")]
        public void BoundTryParseGivesTheErrorTheClauseGives(string text, string clause)
        {
            // Act
            var read = Bound.TryParse(text, clause, out var bound, out var error);
            var query = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog " + clause + " " + text).Error;

            // Assert
            Assert.IsFalse(read);
            Assert.IsNull(bound);
            Assert.AreEqual(query.Message, error.Message);
            Assert.AreEqual(text.Trim(), text.Substring(error.Start, error.Length));
            Assert.ThrowsExactly<ArgumentNullException>(() => Bound.TryParse(null, clause, out _, out _));
            Assert.ThrowsExactly<ArgumentNullException>(() => Bound.TryParse(text, null, out _, out _));
        }

        [TestMethod]
        [DataRow("SELECT count(*) FROM shipslog /* note")]
        [DataRow("SELECT count(*)\nFROM shipslog\n/* note\nmore")]
        [DataRow("SELECT count(*) FROM shipslog WHERE Quantum /*")]
        [DataRow("SELECT count(*) FROM shipslog /* a /* b */")]
        public void UnterminatedBlockCommentIsAnErrorAtTheComment(string nrql)
        {
            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(nrql);
            var thrown = Assert.ThrowsExactly<SqlException>(() => new TimeSeriesQuery<Sample>(nrql, Time));
            var core = SqlSyntaxTree.Parse(nrql).Error;

            // Assert
            Assert.AreEqual(nrql.Substring(nrql.IndexOf("/*", StringComparison.Ordinal)), nrql.Substring(syntax.Error.Start, syntax.Error.Length));
            Assert.AreEqual(SqlErrorKind.UnterminatedComment, syntax.Error.Kind);
            Assert.AreEqual(core.Message, syntax.Error.Message);
            Assert.AreEqual(syntax.Error.Start + "/" + syntax.Error.Length, core.Start + "/" + core.Length);
            Assert.AreEqual(syntax.Error.Message, thrown.Message);
            Assert.AreEqual(SqlTokenKind.Comment, syntax.Tokens[^1].Kind);
        }

        [TestMethod]
        [DataRow("SELECT count(*) FROM shipslog /* note */")]
        [DataRow("SELECT count(*) /* a */ FROM shipslog -- /* b")]
        [DataRow("SELECT count(*) FROM shipslog WHERE Note <> '/*'")]
        public void ClosedOrQuotedCommentIsNoError(string nrql)
        {
            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(nrql);

            // Assert
            Assert.IsNull(syntax.Error);
            Assert.StartsWith("count(*)=", Run(syntax));
        }

        [TestMethod]
        public void EditLeavesOutTheCommentsAtTheEdgesOfItsText()
        {
            // Act
            var edited = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog").Replace("WHERE", "/* a */ Quantum /* x");

            // Assert
            Assert.AreEqual("SELECT count(*) FROM shipslog WHERE Quantum", edited.ToSql());
            Assert.IsNull(edited.Error);
        }

        [TestMethod]
        public void ItemNamesAreTheMembersEachItemReads()
        {
            // Arrange
            const string Nrql = "SELECT max(DriveSpin) - min(PlasmaPressure) AS Spread, count(*) FROM shipslog WHERE Quantum FACET cases(FuelMix < 13 AS 'rich', FuelMix >= 13 AS 'lean'), DriveStage";

            // Act
            var syntax = TimeSeriesSyntaxTree.Parse(Nrql);
            var items = syntax.Select.Concat(syntax.Facets).ToList();
            var edited = syntax.Add("SELECT", " average(FuelMix)");
            var added = edited.Select[2];

            // Assert
            Assert.AreEqual("DriveSpin, PlasmaPressure |  | FuelMix, FuelMix | DriveStage", string.Join(" | ", items.Select(i => string.Join(", ", i.Names.Select(n => n.LookupId)))));
            Assert.IsTrue(items.SelectMany(i => i.Names).All(n => Nrql.Substring(n.Start, n.Length) == n.LookupId));
            CollectionAssert.AreEquivalent(syntax.Names.ToList(), items.SelectMany(i => i.Names).Append(syntax.Names.Single(n => n.LookupId == "Quantum")).ToList());
            Assert.AreEqual("FuelMix@" + edited.Text.IndexOf("FuelMix)", StringComparison.Ordinal), string.Join(", ", added.Names.Select(n => n.LookupId + "@" + n.Start)));
        }

        [TestMethod]
        public void ColumnItemsLeadBackToTheItemsAsWritten()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT max(DriveSpin) AS Top, percentile(FuelMix, 50, 90) FROM shipslog FACET Quantum, buckets(PlasmaPressure, 120, 3) TIMESERIES 1 second");
            var items = syntax.Select.Concat(syntax.Facets).ToList();

            // Act
            var columns = syntax.Compile<Sample>().ColumnDescriptions.Where(c => c.Item != null).ToList();
            var bound = syntax.Rewrite().Bind(Functions);

            // Assert
            Assert.HasCount(5, columns);
            Assert.IsTrue(columns.All(c => items.Contains(c.Item.Original) && !items.Contains(c.Item)));
            CollectionAssert.AreEqual(items, bound.Select.Concat(bound.Facets).Select(i => i.Original).ToList());
            Assert.IsTrue(items.All(i => i.Original == i));
        }

        [TestMethod]
        [DataRow("SELECT count(*) FROM shipslog /* open", SqlErrorKind.UnterminatedComment)]
        [DataRow("SELECT count(*) FROM shipslog WHERE Note = 'Kaylon", SqlErrorKind.UnterminatedString)]
        [DataRow("SELECT count(*) FROM shipslog WHERE DriveSpin ~ 2", SqlErrorKind.Lexical)]
        [DataRow("SELECT count(*) FROM shipslog WHERE DriveSpin >", SqlErrorKind.Syntax)]
        [DataRow("SELECT count(*) FROM shipslog SINCE 1 hou", SqlErrorKind.Syntax)]
        [DataRow("SELECT count(*) FROM shipslog GROUP BY Id", SqlErrorKind.Syntax)]
        [DataRow("SELECT count(* FROM shipslog", SqlErrorKind.UnbalancedParentheses)]
        [DataRow("SELECT count(*) FROM shipslog COMPARE WITH 1 second", SqlErrorKind.Semantic)]
        [DataRow("SELECT Id, count(*) FROM shipslog", SqlErrorKind.Semantic)]
        [DataRow("SELECT max(DriveSpin) FROM shipslog TIMESERIES 1 second SESSION 1 second", SqlErrorKind.Semantic)]
        [DataRow("SELECT max(Warp) FROM shipslog", SqlErrorKind.UnknownName)]
        public void ErrorsSayWhatTheyAre(string nrql, SqlErrorKind kind)
        {
            // Act
            var parsed = TimeSeriesSyntaxTree.Parse(nrql).Error;
            var thrown = Assert.ThrowsExactly<SqlException>(() => new TimeSeriesQuery<Sample>(nrql, Time));

            // Assert
            Assert.AreEqual(kind, thrown.Kind);
            Assert.AreEqual(kind == SqlErrorKind.UnknownName ? null : (SqlErrorKind?)kind, parsed?.Kind);
        }

        [TestMethod]
        [DataRow("SELECT max(DriveSpin) + (pi() FROM shipslog", "(pi()")]
        [DataRow("SELECT count(*) FROM shipslog WHERE (round(DriveSpin, 1) > 1 FACET Quantum", "(round(DriveSpin, 1) > 1")]
        public void UnbalancedParenthesesAroundCallsAreErrorsAtTheirText(string nrql, string text)
        {
            // Act
            var error = TimeSeriesSyntaxTree.Parse(nrql).Error;

            // Assert
            Assert.AreEqual(SqlErrorKind.UnbalancedParentheses, error.Kind);
            Assert.AreEqual(text, nrql.Substring(error.Start, error.Length));
        }

        [TestMethod]
        public void RunAndDurationErrorsSayWhatTheyAre()
        {
            // Arrange
            var query = new TimeSeriesQuery<Sample>("SELECT count(*) FROM shipslog FACET buckets(Note, 10, 2)", Time);

            // Act
            var run = Assert.ThrowsExactly<SqlException>(() => query.Evaluate(GetSamples()));
            Bound.TryParse("5 mintues", "SINCE", out _, out var duration);

            // Assert
            Assert.AreEqual(SqlErrorKind.Evaluation, run.Kind);
            Assert.AreEqual(SqlErrorKind.Syntax, duration.Kind);
        }

        [TestMethod]
        [DataRow("SELECT a FROM d SINCE", "SINCE=1 hour ago", "SELECT a FROM d SINCE 1 hour ago")]
        [DataRow("SELECT a FROM d SINCE LIMIT 5", "SINCE=1 hour ago", "SELECT a FROM d SINCE 1 hour ago LIMIT 5")]
        [DataRow("SELECT a FROM d SINCE -- pick\nLIMIT 5", "SINCE=1 hour ago", "SELECT a FROM d SINCE 1 hour ago -- pick\nLIMIT 5")]
        [DataRow("SELECT a FROM d SINCE   LIMIT 5", "SINCE=1 hour ago", "SELECT a FROM d SINCE 1 hour ago   LIMIT 5")]
        [DataRow("SELECT a FROM d SINCE\r\nLIMIT 5", "SINCE=1 hour ago", "SELECT a FROM d SINCE 1 hour ago\r\nLIMIT 5")]
        [DataRow("SELECT a FROM d SINCE", "SINCE-", "SELECT a FROM d")]
        [DataRow("SELECT a FROM d SINCE LIMIT 5", "SINCE-", "SELECT a FROM d LIMIT 5")]
        [DataRow("SELECT a FROM d SINCE -- pick\nLIMIT 5", "SINCE-", "SELECT a FROM d -- pick\nLIMIT 5")]
        [DataRow("SELECT a FROM d SINCE   LIMIT 5", "SINCE-", "SELECT a FROM d   LIMIT 5")]
        [DataRow("SELECT a FROM d\r\nSINCE\r\nLIMIT 5", "SINCE-", "SELECT a FROM d\r\nLIMIT 5")]
        [DataRow("SELECT count(*) FROM d FACET", "FACET+Quantum", "SELECT count(*) FROM d FACET Quantum")]
        [DataRow("SELECT count(*) FROM d FACET LIMIT 5", "FACET+Quantum", "SELECT count(*) FROM d FACET Quantum LIMIT 5")]
        [DataRow("SELECT count(*) FROM d FACET -- pick\nLIMIT 5", "FACET+Quantum", "SELECT count(*) FROM d FACET Quantum -- pick\nLIMIT 5")]
        [DataRow("SELECT count(*) FROM d FACET   LIMIT 5", "FACET+Quantum", "SELECT count(*) FROM d FACET Quantum   LIMIT 5")]
        [DataRow("SELECT count(*) FROM d FACET\r\nLIMIT 5", "FACET+Quantum", "SELECT count(*) FROM d FACET Quantum\r\nLIMIT 5")]
        [DataRow("SELECT a FROM d WHERE(a)LIMIT 5", "WHERE=b", "SELECT a FROM d WHERE b LIMIT 5")]
        [DataRow("SELECT a FROM [d]LIMIT 5", "SINCE=1", "SELECT a FROM [d] SINCE 1 LIMIT 5")]
        [DataRow("SELECT a FROM [d] WHERE (a)LIMIT 5", "WHERE-", "SELECT a FROM [d] LIMIT 5")]
        public void EditKeepsTheClausesApart(string nrql, string edit, string expected)
        {
            // Act
            var edited = Edited(TimeSeriesSyntaxTree.Parse(nrql), edit);
            var reparsed = TimeSeriesSyntaxTree.Parse(edited.ToSql());

            // Assert
            Assert.AreEqual(expected, edited.ToSql());
            Assert.AreEqual(Describe(reparsed), Describe(edited));
            Assert.AreEqual(Positions(reparsed), Positions(edited));
        }

        [TestMethod]
        public void EditActsOnTheFirstOfDuplicateClauses()
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog SINCE 1 SINCE 2");

            // Act
            var replaced = syntax.Replace("SINCE", "3");
            var removed = syntax.Replace("SINCE", null);

            // Assert
            Assert.AreEqual("SELECT count(*) FROM shipslog SINCE 3 SINCE 2", replaced.ToSql());
            Assert.AreEqual("The query can't be read because it has more than one SINCE clause.", replaced.Error.Message);
            Assert.AreEqual("SELECT count(*) FROM shipslog SINCE 2", removed.ToSql());
            Assert.IsNull(removed.Error);
            Assert.AreEqual(TimeSpan.FromSeconds(2), removed.Since.Offset);
        }

        [TestMethod]
        [DataRow("SELECT+average(FuelMix)")]
        [DataRow("SELECT=count(*)")]
        [DataRow("SINCE=2 seconds ago")]
        [DataRow("WHERE=DriveStage > 2 AND\n  Quantum")]
        [DataRow("WHERE-")]
        [DataRow("UNTIL=3")]
        [DataRow("FACET+buckets(PlasmaPressure, 120, 3)")]
        [DataRow("FACET=cases(DriveStage < 2 AS 'low', DriveStage >= 2 AS 'high')")]
        [DataRow("TIMESERIES=1 second;WHERE=Quantum;SELECT+max(twice(DriveSpin))")]
        [DataRow("SINCE=1 hou")]
        public void EveryPositionOfAnEditedTreeIsInItsText(string edits)
        {
            // Arrange
            var syntax = TimeSeriesSyntaxTree.Parse("SELECT max(DriveSpin) AS Top, count(*)\n  FROM shipslog -- all\n  WHERE DriveStage > 1\n  FACET Quantum\n  SINCE 1 second ago", Functions);

            // Act
            var edited = Edited(syntax, edits);
            var text = edited.ToSql();
            var reparsed = TimeSeriesSyntaxTree.Parse(text, Functions);

            // Assert
            Assert.AreEqual(text, edited.Text);
            Assert.AreEqual(Positions(reparsed), Positions(edited));
            Assert.AreEqual(Describe(reparsed), Describe(edited));
            Assert.IsTrue(edited.Tokens.All(t => text.Substring(t.Start, t.Text.Length) == t.Text));
            Assert.IsTrue(edited.Names.All(n => text.Substring(n.Start, n.Length) == n.LookupId));
            Assert.IsTrue(edited.Select.Concat(edited.Facets).All(i => text.Substring(i.Start, i.Text.Length) == i.Text));
            Assert.IsTrue(edited.Clauses.All(c => text.Substring(c.Start, c.Text.Length) == c.Text && text.Substring(c.KeywordStart, c.Keyword.Length).Equals(c.Keyword, StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        [DataRow("SELECT nofunc(DriveSpin) FROM shipslog")]
        [DataRow("SELECT count(*) FROM shipslog WHERE nofunc(DriveSpin) > 1")]
        [DataRow("SELECT count(*) FROM shipslog FACET nofunc(DriveSpin)")]
        [DataRow("SELECT max(nofunc(DriveSpin)) FROM shipslog")]
        [DataRow("SELECT count(*) FROM shipslog FACET buckets(nofunc(DriveSpin), 10, 2)")]
        [DataRow("SELECT count(*), nofunc(DriveSpin) FROM shipslog")]
        public void UnknownFunctionIsAnUnknownNameAtItsName(string nrql)
        {
            // Act
            var error = TimeSeriesSyntaxTree.Parse(nrql).Error;
            var thrown = Assert.ThrowsExactly<SqlException>(() => new TimeSeriesQuery<Sample>(nrql, Time));

            // Assert
            Assert.AreEqual(SqlErrorKind.UnknownName, error.Kind);
            Assert.AreEqual("nofunc", nrql.Substring(error.Start, error.Length));
            Assert.AreEqual(SqlErrorKind.UnknownName, thrown.Kind);
        }

        [TestMethod]
        [DataRow("1 second ago", "COMPARE WITH")]
        [DataRow("1 second", "COMPARE WITH")]
        [DataRow("1 second", "TIMESERIES")]
        [DataRow("500 ms", "SESSION")]
        [DataRow("1 second ago", "UNTIL")]
        public void BoundTryParseTakesWhatTheClauseTakes(string text, string clause)
        {
            // Act
            var read = Bound.TryParse(text, clause, out var bound, out var error);
            var query = TimeSeriesSyntaxTree.Parse("SELECT count(*) FROM shipslog SINCE 2 " + clause + " " + text).Error;

            // Assert
            Assert.IsTrue(read);
            Assert.IsNull(error);
            Assert.IsNotNull(bound);
            Assert.IsNull(query);
        }
    }
}
