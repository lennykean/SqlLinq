using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesSyntaxTree
    {
        private static readonly Regex ClauseKeyword = new Regex(@"'[^']*'|\[[^\]]*\]|\b(WHERE|FACET|SINCE|UNTIL|TIMESERIES|SESSION|LIMIT|COMPARE|GROUP|HAVING|ORDER)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SelectFrom = new Regex(@"^\s*(SELECT)\s+(.+?)\s+(FROM)\s+(\[[^\]]+\]|\w+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex SourceName = new Regex(@"^(\[[^\]]+\]|\w+)$", RegexOptions.Singleline);
        private static readonly Regex Alias = new Regex(@"^(.+?)\s+AS\s+(\[[^\]]+\]|\w+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex CompareWith = new Regex(@"^WITH\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly string[] Order = { "SELECT", "FROM", "WHERE", "FACET", "SINCE", "UNTIL", "TIMESERIES", "SESSION", "COMPARE", "LIMIT" };

        private IEnumerable<ISqlFunction> _functions;

        private string _text;

        private IReadOnlyList<TimeSeriesExpression> _items;

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

        internal SqlException CommentError { get; private set; }

        internal SqlException SplitError { get; private set; }

        internal SqlException SelectError { get; private set; }

        internal SqlException WhereError => _where?.Error;

        internal SqlException ClauseError { get; private set; }

        internal SqlException FinalError { get; private set; }

        private IEnumerable<SqlException> Errors
        {
            get
            {
                return new[] { CommentError, SplitError, SelectError }
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

            return Parse(nrql, functions, null, 0);
        }

        private static TimeSeriesSyntaxTree Parse(string nrql, IEnumerable<ISqlFunction> functions, TimeSeriesSyntaxTree previous, int at)
        {
            var tokens = SqlSyntax.Tokenize(nrql);
            var tree = new TimeSeriesSyntaxTree
            {
                Text = nrql,
                Tokens = tokens.Select(t => t.Kind == SqlTokenKind.Identifier && Keywords.Contains(t.Text, StringComparer.OrdinalIgnoreCase) ? new SqlToken(SqlTokenKind.Keyword, t.Text, t.Start) : t).ToList(),
                CommentError = Unterminated(nrql, tokens),
                _functions = functions,
                _text = Uncommented(nrql, tokens),
            };
            tree.Read(previous, at);
            tree.Validate();

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

        public TimeSeriesSyntaxTree Replace(string keyword, string text)
        {
            var clause = Editable(keyword);
            if (text == null && (clause == "SELECT" || clause == "FROM"))
            {
                throw new ArgumentException("SELECT and FROM can't be removed.", nameof(text));
            }

            var existing = Clause(clause);
            if (text == null)
            {
                return existing == null ? this : Removed(existing);
            }

            var value = Written(text);
            if (existing == null)
            {
                return Inserted(clause, value);
            }

            var at = existing.Text.Length == 0 ? End(existing) : existing.Start;

            return Edited(at, existing.Text.Length, at > 0 && value.Length > 0 && !char.IsWhiteSpace(Text[at - 1]) ? " " + value : value);
        }

        public TimeSeriesSyntaxTree Add(string keyword, string items)
        {
            var clause = Editable(keyword);
            if (clause != "SELECT" && clause != "FACET")
            {
                throw new ArgumentException("Only SELECT and FACET have items to add to.", nameof(keyword));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var existing = Clause(clause);
            if (existing == null || existing.Text.Length == 0)
            {
                return Replace(clause, items);
            }

            return Edited(End(existing), 0, Appended(Text.Substring(existing.Start, existing.Text.Length), Written(items)));
        }

        public TimeSeriesSyntaxTree Bind(IEnumerable<ISqlFunction> functions)
        {
            if (functions == null)
            {
                throw new ArgumentNullException(nameof(functions));
            }

            var bound = functions.Where(f => !AggregateFactory<object>.Names.Contains(f.Name) && !FacetExpression.BucketNames.Contains(f.Name)).ToList();
            var tree = (TimeSeriesSyntaxTree)MemberwiseClone();
            tree._functions = bound.Concat((_functions ?? Enumerable.Empty<ISqlFunction>()).Where(f => !bound.Any(b => b.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))).ToList();
            tree.Facets = Facets.Select(f => f.Bind(bound)).ToList();
            if (_rewritten)
            {
                tree.Select = Select.Select(c => c.Bind(bound)).ToList();
                tree.Where = (ExpressionNode)Where?.Bind(bound);

                return tree;
            }

            tree._items = _items.Select(c => c.Bind(bound)).ToList();
            tree._where = _where?.Root == null ? _where : _where.Bind(bound);
            tree.Validate();

            return tree;
        }

        public string ToSql()
        {
            return Text;
        }

        public string ToSql(bool normalize)
        {
            if (!normalize)
            {
                return ToSql();
            }

            var parts = new List<string>();
            foreach (var keyword in Order)
            {
                var clause = Clause(keyword);
                if (clause == null)
                {
                    continue;
                }

                var text = keyword == "SELECT" ? string.Join(", ", _items.Select(i => Spaced(i.Written)))
                    : keyword == "FACET" ? string.Join(", ", Facets.Select(f => Spaced(f.Written)))
                    : Spaced(clause.Text);
                parts.Add(keyword + " " + text);
            }

            return string.Join(" ", parts);
        }

        public TimeSeriesPlan<T> Compile<T>()
        {
            return new TimeSeriesPlan<T>(_rewritten ? this : Rewrite());
        }

        internal static SqlException Unbalanced(SqlException error, string text)
        {
            return error?.Kind == SqlErrorKind.UnbalancedParentheses ? new SqlException("Unbalanced parentheses in " + text, error.Kind, error.Start, error.Length) : error;
        }

        internal static SqlException Value(string text, ArgumentNode argument, IEnumerable<ISqlFunction> functions)
        {
            if (argument != null && argument.Expression != null && argument.Error == null && !TimeSeriesFunction.Within(argument.Expression))
            {
                return null;
            }

            var reparsed = argument == null ? SqlSyntaxTree.ParseExpression(string.Empty, functions) : SqlSyntaxTree.ParseExpression(text, argument.Start, argument.Length, functions);

            return reparsed.Error ?? new SqlException(argument.Text + " can't be read inside an aggregate.", SqlErrorKind.Semantic, argument.Start, argument.Length);
        }

        private string Editable(string keyword)
        {
            if (keyword == null)
            {
                throw new ArgumentNullException(nameof(keyword));
            }

            if (_rewritten)
            {
                throw new InvalidOperationException("A rewritten tree can't be edited.");
            }

            var clause = keyword.Trim().ToUpperInvariant();
            if (!Order.Contains(clause))
            {
                throw new ArgumentException(keyword + " isn't a clause of a time series query.", nameof(keyword));
            }

            return clause;
        }

        private TimeSeriesSyntaxTree Edited(int at, int removed, string inserted)
        {
            var end = at + removed;
            var last = inserted.Length > 0 ? inserted[^1] : at > 0 ? Text[at - 1] : ' ';
            var text = end < Text.Length && !char.IsWhiteSpace(Text[end]) && !char.IsWhiteSpace(last) ? inserted + " " : inserted;

            return Parse(Text.Substring(0, at) + text + Text.Substring(end), _functions, this, at);
        }

        private TimeSeriesSyntaxTree Removed(TimeSeriesClause clause)
        {
            var i = Clauses.ToList().IndexOf(clause);
            var before = Gap(i);
            var after = Gap(i + 1);
            var from = clause.KeywordStart - before.Length;

            return Edited(from, End(clause) + after.Length - from, Joined(before, after, i > 0, i + 1 < Clauses.Count));
        }

        private TimeSeriesSyntaxTree Inserted(string keyword, string value)
        {
            var at = Clauses.ToList().FindLastIndex(c => Array.IndexOf(Order, c.Keyword) < Array.IndexOf(Order, keyword)) + 1;
            var gap = Gap(at);
            var next = at < Clauses.Count;
            var newline = Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

            return Edited((next ? Clauses[at].KeywordStart : Text.Length) - gap.Length, gap.Length, Insert(gap, at == 0 ? string.Empty : next ? gap : Gap(at - 1), at == 0, next, keyword + " " + value, newline));
        }

        private string Gap(int index)
        {
            var from = index > 0 ? End(Clauses[index - 1]) : 0;

            return Text.Substring(from, (index < Clauses.Count ? Clauses[index].KeywordStart : Text.Length) - from);
        }

        private static int End(TimeSeriesClause clause)
        {
            return clause.Text.Length == 0 ? clause.KeywordStart + clause.Keyword.Length : clause.Start + clause.Text.Length;
        }

        private static string Written(string text)
        {
            var source = Uncommented(text, SqlSyntax.Tokenize(text));
            var start = source.Length - source.TrimStart().Length;

            return text.Substring(start, source.Trim().Length);
        }

        private static string Joined(string before, string after, bool partBefore, bool partAfter)
        {
            var end = before.LastIndexOf('\n');
            var start = after.IndexOf('\n');
            var preceded = end >= 0 ? !string.IsNullOrWhiteSpace(before.Substring(end + 1)) : partBefore || !string.IsNullOrWhiteSpace(before);
            var followed = start >= 0 ? !string.IsNullOrWhiteSpace(after.Substring(0, start)) : partAfter || !string.IsNullOrWhiteSpace(after);
            if (preceded)
            {
                return before.TrimEnd(' ', '\t') + (followed ? after : after.TrimStart(' ', '\t'));
            }

            if (followed)
            {
                return before + after.TrimStart(' ', '\t');
            }

            if (start >= 0)
            {
                return before.Substring(0, end + 1) + after.Substring(start + 1);
            }

            return before.Substring(0, end > 0 && before[end - 1] == '\r' ? end - 1 : Math.Max(end, 0));
        }

        private static string Insert(string gap, string around, bool first, bool next, string part, string newline)
        {
            if (around.IndexOf('\n') < 0)
            {
                return first ? gap + part + " " : " " + part + gap;
            }

            var line = around.Substring(around.LastIndexOf('\n') + 1);
            var indent = line.Substring(0, line.Length - line.TrimStart().Length);
            var comments = SqlSyntax.Tokenize(gap).Where(t => t.Kind == SqlTokenKind.Comment).ToList();
            var end = gap.IndexOf('\n');
            while (end >= 0 && comments.Any(c => end > c.Start && end < c.Start + c.Text.Length))
            {
                end = gap.IndexOf('\n', end + 1);
            }

            return end < 0 ? gap.TrimEnd(' ', '\t') + newline + indent + part + (next ? " " : string.Empty) : gap.Substring(0, end + 1) + indent + part + newline + gap.Substring(end + 1);
        }

        private static string Appended(string list, string items)
        {
            var tokens = SqlSyntax.Tokenize(list);
            var depth = 0;
            var comma = -1;
            for (var i = 0; i < tokens.Count; i++)
            {
                depth += tokens[i].Text == "(" ? 1 : tokens[i].Text == ")" ? -1 : 0;
                comma = tokens[i].Kind == SqlTokenKind.Operator && tokens[i].Text == "," && depth == 0 ? i : comma;
            }

            if (comma < 0)
            {
                return ", " + items;
            }

            var next = tokens.Skip(comma + 1).FirstOrDefault(t => t.Kind != SqlTokenKind.Comment);
            var space = list.Substring(tokens[comma].Start + 1, (next?.Start ?? list.Length) - tokens[comma].Start - 1);
            var line = space.LastIndexOf('\n');
            var separator = line >= 0 ? (line > 0 && space[line - 1] == '\r' ? ",\r\n" : ",\n") + space.Substring(line + 1) : string.IsNullOrWhiteSpace(space) ? "," + space : ", ";

            return separator + items;
        }

        private static string Spaced(string text)
        {
            var sql = new StringBuilder();
            var end = 0;
            foreach (var token in SqlSyntax.Tokenize(text))
            {
                var gap = text.Substring(end, token.Start - end);
                sql.Append(sql.Length > 0 && gap.Trim().Length == 0 && gap.Length > 0 ? " " : gap.Trim());
                sql.Append(token.Text);
                end = token.Start + token.Text.Length;
            }

            return sql.ToString();
        }

        private static string Uncommented(string text, IReadOnlyList<SqlToken> tokens)
        {
            var uncommented = text.ToCharArray();
            foreach (var comment in tokens.Where(t => t.Kind == SqlTokenKind.Comment))
            {
                for (var i = comment.Start; i < comment.Start + comment.Text.Length; i++)
                {
                    uncommented[i] = uncommented[i] == '\r' || uncommented[i] == '\n' ? uncommented[i] : ' ';
                }
            }

            return new string(uncommented);
        }

        private static SqlException Unterminated(string text, IReadOnlyList<SqlToken> tokens)
        {
            var last = tokens.Count > 0 ? tokens[^1] : null;
            var open = last != null && last.Kind == SqlTokenKind.Comment && last.Text.StartsWith("/*", StringComparison.Ordinal) && SqlSyntax.Tokenize(last.Text + "*/")[0].Text.Length > last.Text.Length;

            return open ? new SqlException("SQL parse error:\n\tComment Error. Unexpected end of input.\nin statement\n\t" + text.Substring(text.LastIndexOf('\n') + 1), SqlErrorKind.UnterminatedComment, last.Start, last.Text.Length) : null;
        }

        private TimeSeriesClause Clause(string keyword)
        {
            return Clauses.FirstOrDefault(c => c.Keyword == keyword);
        }

        private void Read(TimeSeriesSyntaxTree previous, int at)
        {
            var clauses = SplitClauses(_text, out var head, out var error);
            SplitError = error;
            var select = SelectFrom.Match(head);
            Clauses = clauses.Values
                .Concat(select.Success
                    ? new[]
                    {
                        new TimeSeriesClause("SELECT", select.Groups[2].Value, select.Groups[2].Index, select.Groups[1].Index, select.Groups[2].Index + select.Groups[2].Length - select.Groups[1].Index),
                        new TimeSeriesClause("FROM", select.Groups[4].Value, select.Groups[4].Index, select.Groups[3].Index, select.Groups[4].Index + select.Groups[4].Length - select.Groups[3].Index),
                    }
                    : Array.Empty<TimeSeriesClause>())
                .OrderBy(c => c.Start)
                .ToList();
            if (!select.Success)
            {
                SelectError = new SqlException("A time series query starts SELECT <columns> FROM <name>.", SqlErrorKind.Syntax, 0, head.Length);
            }

            Facets = clauses.TryGetValue("FACET", out var facets) ? FacetItems(facets.Text, facets.Start, previous?.Facets, at) : new List<TimeSeriesExpression>();
            _items = select.Success ? Items(select.Groups[2].Value, select.Groups[2].Index, previous?._items, at) : new List<TimeSeriesExpression>();

            if (clauses.TryGetValue("WHERE", out var where))
            {
                var old = previous?.Clause("WHERE");
                _where = old?.Text != where.Text ? SqlSyntaxTree.ParseExpression(_text, where.Start, where.Text.Length, _functions)
                    : old.Start == where.Start && End(old) <= at ? previous._where
                    : previous._where.Shift(_text, where.Start - old.Start);
            }
        }

        private List<TimeSeriesExpression> Items(string list, int start, IEnumerable<TimeSeriesExpression> previous, int at)
        {
            return Split(list, start).Select(i => Reused(previous, i, at) ?? Item(_text, i.Text, i.Start)).ToList();
        }

        private List<TimeSeriesExpression> FacetItems(string list, int start, IEnumerable<TimeSeriesExpression> previous, int at)
        {
            return Split(list, start).Select(f => Reused(previous, f, at) ?? FacetExpression.Read(_text, f.Start, f.Text, _functions)).ToList();
        }

        private TimeSeriesExpression Reused(IEnumerable<TimeSeriesExpression> items, (string Text, int Start) part, int at)
        {
            var item = items?.FirstOrDefault(i => i.Written == part.Text);
            if (item == null || item.Start == part.Start && item.Start + item.Written.Length <= at)
            {
                return item;
            }

            var shifted = item.Shift(_text, part.Start - item.Start);

            return ReferenceEquals(item.Expression, item.Syntax.Root) ? shifted : shifted.Bind(_functions.ToList());
        }

        private TimeSeriesExpression Item(string source, string item, int start)
        {
            var alias = Alias.Match(item);
            var text = alias.Success ? alias.Groups[1].Value.Trim() : item;
            var name = alias.Success ? SqlSyntax.Unbracket(alias.Groups[2].Value) : item;
            var syntax = SqlSyntaxTree.ParseExpression(source, start, text.Length, TimeSeriesFunction.With(AggregateFactory<object>.Names, _functions));

            return new TimeSeriesExpression(text, alias.Success ? name : null, name, start, syntax) { Written = item, Source = source };
        }

        private void Validate()
        {
            Aggregated = _items.Any(i => TimeSeriesFunction.CallsAny(i.Written, AggregateFactory<object>.Names));
            Select = _items.Select(Column).ToList();
            Where = _where?.Root as ExpressionNode;
            Since = null;
            Until = null;
            Compare = null;
            TimeSeries = null;
            Session = null;
            Limit = null;
            ClauseError = null;
            FinalError = null;

            if (Clause("SELECT") != null && Clause("FROM") is TimeSeriesClause from)
            {
                SelectError = SourceName.IsMatch(from.Text) ? null : new SqlException("A time series query starts SELECT <columns> FROM <name>.", SqlErrorKind.Syntax, from.Start, from.Text.Length);
            }

            if (Clause("SINCE") is TimeSeriesClause since)
            {
                Read(() => Since = Bound.Parse(since.Text, "SINCE", since.Start));
            }

            if (Clause("UNTIL") is TimeSeriesClause until)
            {
                Read(() => Until = Bound.Parse(until.Text, "UNTIL", until.Start));
            }

            if (Clause("COMPARE") is TimeSeriesClause compare)
            {
                Read(() => Compare = CompareOffset(compare));
            }

            if (Clause("TIMESERIES") is TimeSeriesClause bucket)
            {
                Read(() => TimeSeries = Bound.Parse(bucket.Text, "TIMESERIES", bucket.Start).Offset);
            }

            if (Clause("SESSION") is TimeSeriesClause session)
            {
                Read(() => Session = SessionGap(session));
            }

            if (Clause("LIMIT") is TimeSeriesClause limit)
            {
                Read(() => Limit = LimitCount(limit));
            }

            if (!Aggregated && (Facets.Count > 0 || TimeSeries != null || Session != null))
            {
                var list = Clause("SELECT");
                FinalError = new SqlException("FACET, TIMESERIES, and SESSION need aggregate functions in SELECT, e.g. average(RPM).", SqlErrorKind.Semantic, list?.Start ?? 0, list?.Text.Length ?? 0);
            }

            Names = Select.SelectMany(c => c.Names).Concat(_where?.Names ?? Array.Empty<Identifier>()).Concat(Facets.SelectMany(f => f.Names)).OrderBy(n => n.Start).ToList();
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
                throw new SqlException("COMPARE WITH needs a duration, e.g. COMPARE WITH 1 minute ago.", SqlErrorKind.Syntax, compare.Start, compare.Text.Length);
            }
            if (Since == null)
            {
                throw new SqlException("COMPARE WITH needs a SINCE window to compare.", SqlErrorKind.Semantic, compare.Start, compare.Text.Length);
            }

            return Bound.Parse(with.Groups[1].Value, "COMPARE WITH", compare.Start + with.Groups[1].Index).Offset;
        }

        private TimeSpan SessionGap(TimeSeriesClause session)
        {
            var gap = Bound.Parse(session.Text, "SESSION", session.Start);
            if (TimeSeries != null)
            {
                throw new SqlException("SESSION and TIMESERIES can't be used together.", SqlErrorKind.Semantic, session.Start, session.Text.Length);
            }

            return gap.Offset;
        }

        private int LimitCount(TimeSeriesClause limit)
        {
            if (!int.TryParse(limit.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
            {
                throw new SqlException("LIMIT needs a positive whole number.", SqlErrorKind.Syntax, limit.Start, limit.Text.Length);
            }
            if (Aggregated && Facets.Count == 0)
            {
                throw new SqlException("LIMIT needs FACET (top facets) or a SELECT without aggregates (first rows).", SqlErrorKind.Semantic, limit.Start, limit.Text.Length);
            }

            return n;
        }

        private TimeSeriesExpression Column(TimeSeriesExpression item)
        {
            var column = item.Copy();
            var text = column.Text;
            var start = column.Start;
            var syntax = column.Syntax;
            column.Error = null;
            column.Aggregates = Array.Empty<NodeWithId>();
            column.Whole = false;

            if (text.Length == 0)
            {
                column.Error = new SqlException("SELECT has an empty column.", SqlErrorKind.Syntax, start, column.Written.Length);
            }
            else if (!Aggregated)
            {
                column.Error = syntax.Error;
            }
            else if (!TimeSeriesFunction.CallsAny(text, AggregateFactory<object>.Names))
            {
                var unbracketed = SqlSyntax.Unbracket(text);
                column.Columns = Array.Empty<string>();
                if (!Facets.Any(f => f.Text.Equals(text, StringComparison.OrdinalIgnoreCase) || f.Name.Equals(unbracketed, StringComparison.OrdinalIgnoreCase)))
                {
                    column.Error = new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' isn't an aggregate. Alongside aggregates, plain columns must be FACET fields.", text), SqlErrorKind.Semantic, start, text.Length);
                }
            }
            else if (syntax.Root == null)
            {
                column.Error = Unbalanced(syntax.Error, column.Written);
            }
            else
            {
                column.Aggregates = TimeSeriesFunction.Calls(column.Expression, AggregateFactory<object>.Names);
                column.Whole = column.Aggregates.Count == 1 && column.Aggregates[0] == column.Expression;
                column.Error = Check(column);
                if (column.Whole && column.Expression is NodeWithId call && call.FunctionName.Equals("percentile", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 2)
                {
                    column.Columns = call.Arguments.Skip(1).Select(p => column.Alias != null ? column.Alias + "." + p.Text : string.Format(CultureInfo.CurrentCulture, "percentile({0}, {1})", call.Arguments[0].Text, p.Text)).ToList();
                }
            }

            return column;
        }

        private SqlException Check(TimeSeriesExpression column)
        {
            var check = new AggregateFactory<object>(argument => Value(column.Source, argument, _functions) is SqlException error ? throw error : (System.Linq.Expressions.Expression<Func<object, object>>)null);
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
                return e.Start < 0 ? new SqlException(e.Message, e.InnerException, e.Kind, column.Start, column.Text.Length) : e;
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
                    error ??= new SqlException(keyword + " isn't supported in a time series query. Use FACET to group.", SqlErrorKind.Syntax, keywords[i].Index, keywords[i].Length);
                    continue;
                }

                var start = keywords[i].Index + keywords[i].Length;
                var end = i + 1 < keywords.Count ? keywords[i + 1].Index : nrql.Length;
                var text = nrql.Substring(start, end - start);
                var trimmed = text.Trim();
                var textStart = start + text.Length - text.TrimStart().Length;
                if (!clauses.TryAdd(keyword, new TimeSeriesClause(keyword, trimmed, textStart, keywords[i].Index, textStart + trimmed.Length - keywords[i].Index)))
                {
                    error ??= new SqlException("The query has more than one " + keyword + " clause.", SqlErrorKind.Syntax, keywords[i].Index, keywords[i].Length);
                }
            }

            head = keywords.Count > 0 ? nrql.Substring(0, keywords[0].Index) : nrql;
            return clauses;
        }
    }
}
