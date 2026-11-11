using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesSyntaxTree
    {
        private static readonly Regex ClauseKeyword = new Regex(@"'[^']*'|\[[^\]]*\]|\b(WHERE|FACET|SINCE|UNTIL|TIMESERIES|SESSION|LIMIT|COMPARE|GROUP|HAVING|ORDER)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SelectFrom = new Regex(@"^\s*SELECT\s+(.+?)\s+FROM\s+(\[[^\]]+\]|\w+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex Alias = new Regex(@"^(.+?)\s+AS\s+(\[[^\]]+\]|\w+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex CompareWith = new Regex(@"^WITH\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        private IEnumerable<ISqlFunction> _functions;

        private string _text;

        private SqlSyntaxTree _where;

        private bool _rewritten;

        private TimeSeriesSyntaxTree()
        {
        }

        public static IReadOnlyList<string> Keywords { get; } = Array.AsReadOnly(new[] { "FACET", "SINCE", "UNTIL", "TIMESERIES", "SESSION", "LIMIT", "COMPARE", "WITH", "AGO" });

        public static IReadOnlyList<string> Aggregates { get; } = AggregateFactory<object>.Names.ToList().AsReadOnly();

        public static IReadOnlyList<string> Functions { get; } = FacetExpression.BucketNames.ToList().AsReadOnly();

        public string Text { get; private set; }

        public IReadOnlyList<SqlToken> Tokens { get; private set; }

        public IReadOnlyList<TimeSeriesClause> Clauses { get; private set; }

        public IReadOnlyList<TimeSeriesExpression> Select { get; private set; }

        public IReadOnlyList<TimeSeriesExpression> Facets { get; private set; }

        public ExpressionNode Where { get; private set; }

        public Bound Since { get; private set; }

        public Bound Until { get; private set; }

        public TimeSpan? TimeSeries { get; private set; }

        public TimeSpan? Session { get; private set; }

        public TimeSpan? Compare { get; private set; }

        public int? Limit { get; private set; }

        public IReadOnlyList<Identifier> Names { get; private set; }

        public SqlException Error => Errors.FirstOrDefault(e => e != null);

        internal bool Aggregated { get; private set; }

        internal SqlException SplitError { get; private set; }

        internal SqlException SelectError { get; private set; }

        internal SqlException WhereError => _where?.Error;

        internal SqlException ClauseError { get; private set; }

        internal SqlException FinalError { get; private set; }

        private IEnumerable<SqlException> Errors
        {
            get
            {
                return new[] { SplitError, SelectError }
                    .Concat(Facets.Select(f => f.Error))
                    .Concat(Select.Select(c => c.Error))
                    .Concat(new[] { WhereError, ClauseError, FinalError });
            }
        }

        public static TimeSeriesSyntaxTree Parse(string nrql)
        {
            return Parse(nrql, null);
        }

        public static TimeSeriesSyntaxTree Parse(string nrql, IEnumerable<ISqlFunction> functions)
        {
            if (nrql == null)
            {
                throw new ArgumentNullException(nameof(nrql));
            }

            var tokens = SqlSyntax.Tokenize(nrql);
            var text = nrql.ToCharArray();
            foreach (var comment in tokens.Where(t => t.Kind == SqlTokenKind.Comment))
            {
                for (var i = comment.Start; i < comment.Start + comment.Text.Length; i++)
                {
                    text[i] = text[i] == '\r' || text[i] == '\n' ? text[i] : ' ';
                }
            }

            var tree = new TimeSeriesSyntaxTree
            {
                Text = nrql,
                Tokens = tokens.Select(t => t.Kind == SqlTokenKind.Identifier && Keywords.Contains(t.Text, StringComparer.OrdinalIgnoreCase) ? new SqlToken(SqlTokenKind.Keyword, t.Text, t.Start) : t).ToList(),
                _functions = functions,
                _text = new string(text),
            };
            tree.Read();

            return tree;
        }

        public TimeSeriesSyntaxTree Rewrite()
        {
            return Rewrite(null);
        }

        public TimeSeriesSyntaxTree Rewrite(Func<ExpressionNode, ExpressionNode> rewrite)
        {
            if (_rewritten)
            {
                throw new InvalidOperationException("The tree is already rewritten.");
            }

            Func<ExpressionNode, ExpressionNode> hook = expression => rewrite?.Invoke(expression) ?? expression;
            var tree = (TimeSeriesSyntaxTree)MemberwiseClone();
            tree._rewritten = true;
            tree.Facets = Facets.Select(f => f.Error != null ? f : Rewritten(f, hook)).ToList();
            tree.Select = Select.Select(c => c.Error != null || c.Expression == null ? c : Rewritten(c, hook)).ToList();
            tree.Where = Where == null || WhereError != null ? Where : hook(Where);

            return tree;
        }

        public TimeSeriesPlan<T> Compile<T>()
        {
            return new TimeSeriesPlan<T>(_rewritten ? this : Rewrite());
        }

        internal static SqlException Unbalanced(SqlException error, string text)
        {
            return error != null && error.Message.StartsWith("Unbalanced parentheses in ", StringComparison.Ordinal) ? new SqlException("Unbalanced parentheses in " + text, error.Start, error.Length) : error;
        }

        internal static SqlException Value(string text, ArgumentNode argument, IEnumerable<ISqlFunction> functions)
        {
            if (argument != null && argument.Expression != null && argument.Error == null && !TimeSeriesFunction.Within(argument.Expression))
            {
                return null;
            }

            var reparsed = argument == null ? SqlSyntaxTree.ParseExpression(string.Empty, functions) : SqlSyntaxTree.ParseExpression(text, argument.Start, argument.Length, functions);

            return reparsed.Error ?? new SqlException(argument.Text + " can't be read inside an aggregate.", argument.Start, argument.Length);
        }

        private void Read()
        {
            var clauses = SplitClauses(_text, out var head, out var error);
            SplitError = error;
            var select = SelectFrom.Match(head);
            Clauses = clauses.Values
                .Concat(select.Success ? new[] { new TimeSeriesClause("SELECT", select.Groups[1].Value, select.Groups[1].Index), new TimeSeriesClause("FROM", select.Groups[2].Value, select.Groups[2].Index) } : Array.Empty<TimeSeriesClause>())
                .OrderBy(c => c.Start)
                .ToList();
            if (!select.Success)
            {
                SelectError = new SqlException("A time series query starts SELECT <columns> FROM <name>.", 0, head.Length);
            }

            Facets = clauses.TryGetValue("FACET", out var facets)
                ? Split(facets.Text, facets.Start).Select(f => FacetExpression.Read(_text, f.Start, f.Text, _functions)).ToList()
                : new List<TimeSeriesExpression>();

            var items = select.Success ? Split(select.Groups[1].Value, select.Groups[1].Index) : new List<(string Text, int Start)>();
            Aggregated = items.Any(i => TimeSeriesFunction.CallsAny(i.Text, AggregateFactory<object>.Names));
            Select = items.Select(i => Column(i.Text, i.Start)).ToList();

            if (clauses.TryGetValue("WHERE", out var where))
            {
                _where = SqlSyntaxTree.ParseExpression(_text, where.Start, where.Text.Length, _functions);
                Where = _where.Root as ExpressionNode;
            }

            if (clauses.TryGetValue("SINCE", out var since))
            {
                Read(() => Since = Bound.Parse(since.Text, "SINCE", since.Start));
            }

            if (clauses.TryGetValue("UNTIL", out var until))
            {
                Read(() => Until = Bound.Parse(until.Text, "UNTIL", until.Start));
            }

            if (clauses.TryGetValue("COMPARE", out var compare))
            {
                Read(() => Compare = CompareOffset(compare));
            }

            if (clauses.TryGetValue("TIMESERIES", out var bucket))
            {
                Read(() => TimeSeries = BucketSize(bucket));
            }

            if (clauses.TryGetValue("SESSION", out var session))
            {
                Read(() => Session = SessionGap(session));
            }

            if (clauses.TryGetValue("LIMIT", out var limit))
            {
                Read(() => Limit = LimitCount(limit));
            }

            if (!Aggregated && (Facets.Count > 0 || TimeSeries != null || Session != null))
            {
                FinalError = new SqlException("FACET, TIMESERIES, and SESSION need aggregate functions in SELECT, e.g. average(RPM).", select.Groups[1].Index, select.Groups[1].Length);
            }

            Names = Facets.SelectMany(f => f.Names)
                .Concat(Select.SelectMany(c => c.Names))
                .Concat(_where?.Names ?? Enumerable.Empty<Identifier>())
                .OrderBy(n => n.Start)
                .ToList();
        }

        private void Read(Action read)
        {
            try
            {
                read();
            }
            catch (SqlException e)
            {
                ClauseError ??= e;
            }
        }

        private TimeSpan CompareOffset(TimeSeriesClause compare)
        {
            var with = CompareWith.Match(compare.Text);
            if (!with.Success)
            {
                throw new SqlException("COMPARE WITH needs a duration, e.g. COMPARE WITH 1 minute ago.", compare.Start, compare.Text.Length);
            }
            if (Since == null)
            {
                throw new SqlException("COMPARE WITH needs a SINCE window to compare.", compare.Start, compare.Text.Length);
            }

            return Bound.Parse(with.Groups[1].Value, "COMPARE WITH", compare.Start + with.Groups[1].Index).Offset;
        }

        private static TimeSpan BucketSize(TimeSeriesClause bucket)
        {
            var size = Bound.Parse(bucket.Text, "TIMESERIES", bucket.Start);
            if (size.Ago || size.Offset <= TimeSpan.Zero)
            {
                throw new SqlException("TIMESERIES needs a positive duration, e.g. TIMESERIES 1 second.", bucket.Start, bucket.Text.Length);
            }

            return size.Offset;
        }

        private TimeSpan SessionGap(TimeSeriesClause session)
        {
            var gap = Bound.Parse(session.Text, "SESSION", session.Start);
            if (gap.Ago || gap.Offset <= TimeSpan.Zero)
            {
                throw new SqlException("SESSION needs a positive gap, e.g. SESSION 1 second.", session.Start, session.Text.Length);
            }
            if (TimeSeries != null)
            {
                throw new SqlException("SESSION and TIMESERIES can't be used together.", session.Start, session.Text.Length);
            }

            return gap.Offset;
        }

        private int LimitCount(TimeSeriesClause limit)
        {
            if (!int.TryParse(limit.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
            {
                throw new SqlException("LIMIT needs a positive whole number.", limit.Start, limit.Text.Length);
            }
            if (Aggregated && Facets.Count == 0)
            {
                throw new SqlException("LIMIT needs FACET (top facets) or a SELECT without aggregates (first rows).", limit.Start, limit.Text.Length);
            }

            return n;
        }

        private TimeSeriesExpression Column(string item, int start)
        {
            var alias = Alias.Match(item);
            var text = alias.Success ? alias.Groups[1].Value.Trim() : item;
            var name = alias.Success ? SqlSyntax.Unbracket(alias.Groups[2].Value) : item;
            var syntax = SqlSyntaxTree.ParseExpression(_text, start, text.Length, TimeSeriesFunction.With(AggregateFactory<object>.Names, _functions));
            var column = new TimeSeriesExpression(text, alias.Success ? name : null, name, start, syntax);

            if (text.Length == 0)
            {
                column.Error = new SqlException("SELECT has an empty column.", start, item.Length);
            }
            else if (!Aggregated)
            {
                column.Error = syntax.Error;
            }
            else if (!TimeSeriesFunction.CallsAny(text, AggregateFactory<object>.Names))
            {
                var unbracketed = SqlSyntax.Unbracket(text);
                if (!Facets.Any(f => f.Text.Equals(text, StringComparison.OrdinalIgnoreCase) || f.Name.Equals(unbracketed, StringComparison.OrdinalIgnoreCase)))
                {
                    column.Error = new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' isn't an aggregate. Alongside aggregates, plain columns must be FACET fields.", text), start, text.Length);
                }
            }
            else if (syntax.Root == null)
            {
                column.Error = Unbalanced(syntax.Error, item);
            }
            else
            {
                column.Aggregates = TimeSeriesFunction.Calls(column.Expression, AggregateFactory<object>.Names);
                column.Whole = column.Aggregates.Count == 1 && column.Aggregates[0] == column.Expression;
                column.Error = Check(column);
            }

            return column;
        }

        private SqlException Check(TimeSeriesExpression column)
        {
            var check = new AggregateFactory<object>(argument => Value(_text, argument, _functions) is SqlException error ? throw error : (System.Linq.Expressions.Expression<Func<object, object>>)null);
            try
            {
                foreach (var call in column.Aggregates)
                {
                    if (column.Whole && call.FunctionName.Equals("percentile", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 2)
                    {
                        foreach (var p in call.Arguments.Skip(1))
                        {
                            check.Create("percentile", new[] { call.Arguments[0], p });
                        }
                    }
                    else
                    {
                        check.Create(call);
                    }
                }

                return null;
            }
            catch (SqlException e)
            {
                return e.Start < 0 ? new SqlException(e.Message, e.InnerException, column.Start, column.Text.Length) : e;
            }
        }

        private static List<(string Text, int Start)> Split(string text, int start)
        {
            var parts = new List<(string Text, int Start)>();
            var depth = 0;
            var from = 0;
            foreach (var token in SqlSyntax.Tokenize(text).Where(t => t.Kind == SqlTokenKind.Operator))
            {
                if (token.Text == "(")
                {
                    depth++;
                }
                else if (token.Text == ")")
                {
                    depth--;
                }
                else if (token.Text == "," && depth == 0)
                {
                    parts.Add(Part(text, start, from, token.Start));
                    from = token.Start + 1;
                }
            }

            parts.Add(Part(text, start, from, text.Length));

            return parts;
        }

        private static (string Text, int Start) Part(string text, int start, int from, int to)
        {
            var part = text.Substring(from, to - from);

            return (part.Trim(), start + from + part.Length - part.TrimStart().Length);
        }

        internal static TimeSeriesExpression Rewritten(TimeSeriesExpression item, Func<ExpressionNode, ExpressionNode> hook)
        {
            if (item.Plain)
            {
                return item.Rewrite(hook(item.Expression), item.Aggregates, item.Conditions);
            }

            if (item.CaseLabels != null)
            {
                return item.Rewrite(item.Expression, item.Aggregates, item.Conditions.Select(hook).ToList());
            }

            if (item.Boundaries != null)
            {
                var value = ((NodeWithId)item.Expression).Arguments[0].Expression;

                return item.Rewrite((ExpressionNode)Replaced(item.Expression, value, hook(value)), item.Aggregates, item.Conditions);
            }

            if (item.Aggregates.Count == 0)
            {
                return item.Rewrite(hook(item.Expression), item.Aggregates, item.Conditions);
            }

            var calls = item.Aggregates.ToList();
            var outer = (ExpressionNode)item.Expression.Replace(n => n is NodeWithId call && calls.Contains(call) ? Reference(calls.IndexOf(call)) : null);

            return item.Rewrite(item.Whole ? outer : hook(outer), calls.Select(c => Lifted(c, hook)).ToList(), item.Conditions);
        }

        private static NodeWithId Lifted(NodeWithId call, Func<ExpressionNode, ExpressionNode> hook)
        {
            var first = call.Arguments.Count > 0 ? call.Arguments[0] : null;
            if (call.FunctionName.Equals("rate", StringComparison.OrdinalIgnoreCase))
            {
                var inner = (NodeWithId)first.Expression;

                return (NodeWithId)Replaced(call, inner, Lifted(inner, hook));
            }

            var value = first == null || first.Text == "*" ? null : first.Expression;

            return value == null ? call : (NodeWithId)Replaced(call, value, hook(value));
        }

        private static NonTerminalNode Replaced(NonTerminalNode node, ExpressionNode value, ExpressionNode replacement)
        {
            return replacement == value ? node : node.Replace(n => n == value ? replacement : null);
        }

        private static ExpressionNode Reference(int index)
        {
            return (ExpressionNode)SqlSyntaxTree.ParseExpression("agg" + index.ToString(CultureInfo.InvariantCulture)).Root;
        }

        private static Dictionary<string, TimeSeriesClause> SplitClauses(string nrql, out string head, out SqlException error)
        {
            var clauses = new Dictionary<string, TimeSeriesClause>();
            var keywords = ClauseKeyword.Matches(nrql).Where(m => m.Groups[1].Success).ToList();
            error = null;

            for (var i = 0; i < keywords.Count; i++)
            {
                var keyword = keywords[i].Groups[1].Value.ToUpperInvariant();
                if (keyword == "GROUP" || keyword == "HAVING" || keyword == "ORDER")
                {
                    error ??= new SqlException(keyword + " isn't supported in a time series query. Use FACET to group.", keywords[i].Index, keywords[i].Length);
                    continue;
                }

                var start = keywords[i].Index + keywords[i].Length;
                var end = i + 1 < keywords.Count ? keywords[i + 1].Index : nrql.Length;
                var text = nrql.Substring(start, end - start);
                if (!clauses.TryAdd(keyword, new TimeSeriesClause(keyword, text.Trim(), start + text.Length - text.TrimStart().Length)))
                {
                    error ??= new SqlException("The query has more than one " + keyword + " clause.", keywords[i].Index, keywords[i].Length);
                }
            }

            head = keywords.Count > 0 ? nrql.Substring(0, keywords[0].Index) : nrql;
            return clauses;
        }
    }
}
