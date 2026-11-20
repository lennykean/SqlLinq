using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using MoreLinq;

namespace SqlLinq.TimeSeries
{
    sealed class TimeSeriesQueryTree<T>
    {
        private static readonly Type RowType = typeof((TimeSpan Time, T Item));
        private static readonly Type RowsType = typeof(IEnumerable<(TimeSpan Time, T Item)>);
        private static readonly Type GroupType = typeof((object[] Key, IEnumerable<(TimeSpan Time, T Item)> Rows));
        private static readonly Type GroupsType = typeof(List<(object[] Key, IEnumerable<(TimeSpan Time, T Item)> Rows)>);
        private static readonly Type LookupType = typeof(ILookup<long, (TimeSpan Time, T Item)>);
        private static readonly Type ResultType = typeof(IDictionary<string, object>);
        private static readonly Type ResultsType = typeof(IList<IDictionary<string, object>>);
        private static readonly Type WindowType = typeof(Func<TimeSpan, TimeSpan, string, List<IDictionary<string, object>>>);
        private const int MaxBuckets = 1000000;

        private readonly ParameterExpression _source = Expression.Parameter(typeof(IEnumerable<T>), "source");
        private readonly ParameterExpression _time = Expression.Parameter(typeof(Func<T, TimeSpan>), "time");
        private readonly ParameterExpression _cancellation = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        private readonly ParameterExpression _all = Expression.Variable(RowsType, "all");
        private readonly List<(string Name, Expression<Func<T, object>> Value)> _facets = new List<(string, Expression<Func<T, object>>)>();
        private readonly List<(string Name, Aggregate Aggregate)> _aggregates = new List<(string, Aggregate)>();
        private readonly List<(string Name, Expression<Func<T, object>> Value)> _columns = new List<(string, Expression<Func<T, object>>)>();
        private readonly List<TimeSeriesColumn> _items = new List<TimeSeriesColumn>();
        private readonly Expression<Func<T, object>> _where;
        private readonly Bound _since;
        private readonly Bound _until;
        private readonly TimeSpan? _compare;
        private readonly long? _bucketTicks;
        private readonly TimeSeriesClause _buckets;
        private readonly TimeSpan? _gap;
        private readonly int? _limit;

        internal TimeSeriesQueryTree(TimeSeriesSyntaxTree syntax)
        {
            Throw(syntax.CommentError);
            Throw(syntax.SplitError);
            Throw(syntax.SelectError);
            foreach (var facet in syntax.Facets)
            {
                Throw(facet.Error);
                _facets.Add((facet.Name, Placed(() => FacetExpression.Lambda<T>(facet), facet.Start, facet.Text.Length)));
                _items.Add(new TimeSeriesColumn(facet.Name, TimeSeriesColumnKind.Facet, facet));
            }

            var factory = new AggregateFactory<T>(argument => Placed(() => SqlExpression.Lambda<T>(argument.Expression), argument.Start, argument.Length));
            foreach (var column in syntax.Select)
            {
                Throw(column.Error);
                AddColumn(syntax, column, factory);
            }

            Throw(syntax.WhereError);
            if (syntax.Where != null)
            {
                _where = Placed(() => SqlExpression.Lambda<T>(syntax.Where), syntax.Where.Start, syntax.Where.Length);
            }

            Throw(syntax.ClauseError);
            Throw(syntax.FinalError);
            _since = syntax.Since;
            _until = syntax.Until;
            _compare = syntax.Compare;
            _bucketTicks = syntax.TimeSeries?.Ticks;
            _buckets = syntax.Clauses.FirstOrDefault(c => c.Keyword == "TIMESERIES");
            _gap = syntax.Session;
            _limit = syntax.Limit;
            Tree = Query();
        }

        internal Expression<Func<IEnumerable<T>, Func<T, TimeSpan>, CancellationToken, IList<IDictionary<string, object>>>> Tree { get; }

        internal IReadOnlyList<TimeSeriesColumn> Columns
        {
            get
            {
                var comparison = _compare == null ? Array.Empty<TimeSeriesColumn>() : new[] { new TimeSeriesColumn("Comparison", TimeSeriesColumnKind.Comparison, null) };
                if (_aggregates.Count == 0)
                {
                    return Distinct(comparison.Append(new TimeSeriesColumn("Time", TimeSeriesColumnKind.Time, null)).Concat(_items.Where(c => c.Kind == TimeSeriesColumnKind.Select)));
                }

                var window = _bucketTicks == null && _gap == null ? Array.Empty<TimeSeriesColumn>() : new[] { new TimeSeriesColumn("BeginTime", TimeSeriesColumnKind.Time, null), new TimeSeriesColumn("EndTime", TimeSeriesColumnKind.Time, null) };

                return Distinct(window.Concat(comparison).Concat(_items));
            }
        }

        internal static Expression Inline(LambdaExpression lambda, Expression argument)
        {
            return new Substitution(lambda.Parameters[0], argument).Visit(lambda.Body);
        }

        internal static Expression IsTrue(Expression value)
        {
            if (value is UnaryExpression boxed && boxed.NodeType == ExpressionType.Convert && (boxed.Operand.Type == typeof(bool) || boxed.Operand.Type == typeof(bool?)))
            {
                return boxed.Operand.Type == typeof(bool) ? boxed.Operand : Expression.Equal(boxed.Operand, Expression.Constant(true, typeof(bool?)));
            }

            return Expression.Call(typeof(Convert), nameof(Convert.ToBoolean), null, value);
        }

        private static void Throw(SqlException error)
        {
            if (error != null)
            {
                throw error;
            }
        }

        private static TResult Placed<TResult>(Func<TResult> compile, int start, int length)
        {
            try
            {
                return compile();
            }
            catch (SqlException e) when (e.Start < 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, start, length);
            }
        }

        private static List<TimeSeriesColumn> Distinct(IEnumerable<TimeSeriesColumn> columns)
        {
            return columns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(g => new TimeSeriesColumn(g.Key, g.Last().Kind, g.Last().Item)).ToList();
        }

        private void AddColumn(TimeSeriesSyntaxTree syntax, TimeSeriesExpression column, AggregateFactory<T> factory)
        {
            if (!syntax.Aggregated)
            {
                _columns.Add((column.Name, Placed(() => SqlExpression.Lambda<T>(column.Expression), column.Start, column.Text.Length)));
                _items.Add(new TimeSeriesColumn(column.Name, TimeSeriesColumnKind.Select, column));
                return;
            }

            if (column.Aggregates.Count == 0)
            {
                return;
            }

            _items.AddRange(column.Columns.Select(c => new TimeSeriesColumn(c, TimeSeriesColumnKind.Select, column)));

            if (column.Whole)
            {
                var call = column.Aggregates[0];
                if (call.FunctionName.Equals("percentile", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 2)
                {
                    _aggregates.AddRange(call.Arguments.Skip(1).Select((p, i) => (column.Columns[i], factory.Create("percentile", new[] { call.Arguments[0], p }))));
                }
                else
                {
                    _aggregates.Add((column.Name, factory.Create(call)));
                }

                return;
            }

            var inner = column.Aggregates.Select(factory.Create).ToList();
            var outer = SqlExpression.Lambda<IDictionary<string, double>>(column.Expression);
            _aggregates.Add((column.Name, (rows, window) => Combined(inner, outer, rows, window)));
        }

        private Expression<Func<IEnumerable<T>, Func<T, TimeSpan>, CancellationToken, IList<IDictionary<string, object>>>> Query()
        {
            var end = Expression.Variable(typeof(TimeSpan), "end");
            var from = Expression.Variable(typeof(TimeSpan), "from");
            var to = Expression.Variable(typeof(TimeSpan), "to");
            var window = Expression.Variable(WindowType, "window");
            var results = _compare == null
                ? (Expression)Expression.Invoke(window, from, to, Expression.Constant(null, typeof(string)))
                : Linq(nameof(Enumerable.ToList), ResultType, Linq(nameof(Enumerable.Concat), ResultType,
                    Expression.Invoke(window, from, to, Expression.Constant("current")),
                    Expression.Invoke(window, Expression.Subtract(from, Expression.Constant(_compare.Value)), Expression.Subtract(to, Expression.Constant(_compare.Value)), Expression.Constant("previous"))));
            var query = Expression.Block(
                new[] { end, from, to, window },
                Expression.Assign(end, Linq(nameof(Enumerable.Max), new[] { RowType, typeof(TimeSpan) }, _all, Times())),
                Expression.Assign(from, _since == null ? Linq(nameof(Enumerable.Min), new[] { RowType, typeof(TimeSpan) }, _all, Times()) : Resolve(_since, end)),
                Expression.Assign(to, _until == null ? end : Resolve(_until, end)),
                Expression.Assign(window, Window()),
                Expression.Convert(results, ResultsType));
            var item = Expression.Parameter(typeof(T), "item");
            var read = Expression.Lambda(Expression.Block(Checked(), Expression.New(RowType.GetConstructor(new[] { typeof(TimeSpan), typeof(T) }), Expression.Invoke(_time, item), item)), item);
            var collection = Expression.OrElse(Expression.TypeIs(_source, typeof(ICollection<T>)), Expression.TypeIs(_source, typeof(IReadOnlyCollection<T>)));
            var body = Expression.Block(
                new[] { _all },
                Checked(),
                Expression.Assign(_all, Expression.Condition(collection, Linq(nameof(Enumerable.Select), new[] { typeof(T), RowType }, _source, read), Expression.Call(typeof(Values), nameof(Values.Read), new[] { typeof(T) }, _source, _time, _cancellation), RowsType)),
                Expression.Condition(
                    Expression.Not(Linq(nameof(Enumerable.Any), RowType, _all)),
                    Expression.Convert(Expression.New(typeof(List<IDictionary<string, object>>)), ResultsType),
                    query,
                    ResultsType));

            return Expression.Lambda<Func<IEnumerable<T>, Func<T, TimeSpan>, CancellationToken, IList<IDictionary<string, object>>>>(body, _source, _time, _cancellation);
        }

        private LambdaExpression Window()
        {
            var from = Expression.Parameter(typeof(TimeSpan), "from");
            var to = Expression.Parameter(typeof(TimeSpan), "to");
            var comparison = Expression.Parameter(typeof(string), "comparison");
            var rows = Expression.Variable(RowsType, "rows");
            var row = Expression.Parameter(RowType, "row");
            var after = _since?.Ago == true ? Expression.GreaterThan(Time(row), from) : Expression.GreaterThanOrEqual(Time(row), from);
            var before = _until == null || _until.Ago ? Expression.LessThanOrEqual(Time(row), to) : Expression.LessThan(Time(row), to);
            Expression inWindow = Linq(nameof(Enumerable.Where), RowType, _all, Expression.Lambda(Expression.AndAlso(after, before), row));
            if (_where != null)
            {
                var kept = Expression.Parameter(RowType, "row");
                inWindow = Linq(nameof(Enumerable.Where), RowType, inWindow, Expression.Lambda(IsTrue(Inline(_where, Item(kept))), kept));
            }

            var body = Expression.Block(
                new[] { rows },
                Expression.Assign(rows, inWindow),
                _aggregates.Count == 0 ? (Expression)Items(rows, comparison) : Grouped(rows, from, to, comparison));

            return Expression.Lambda(WindowType, body, from, to, comparison);
        }

        private MethodCallExpression Items(Expression rows, Expression comparison)
        {
            Expression ordered = Linq(nameof(Enumerable.OrderBy), new[] { RowType, typeof(TimeSpan) }, rows, Times());
            if (_limit != null)
            {
                ordered = Linq(nameof(Enumerable.Take), RowType, ordered, Expression.Constant(_limit.Value));
            }

            var row = Expression.Parameter(RowType, "row");
            var values = new List<(string Name, Expression Value)>();
            if (_compare != null)
            {
                values.Add(("Comparison", comparison));
            }

            values.Add(("Time", Time(row)));
            values.AddRange(_columns.Select(c => (c.Name, Inline(c.Value, Item(row)))));

            return Linq(nameof(Enumerable.ToList), ResultType, Linq(nameof(Enumerable.Select), new[] { RowType, ResultType }, ordered, Expression.Lambda(Dictionary(values), row)));
        }

        private BlockExpression Grouped(Expression rows, Expression from, Expression to, Expression comparison)
        {
            var window = Expression.Variable(typeof(TimeSpan), "window");
            var groups = Expression.Variable(GroupsType, "groups");
            var group = Expression.Parameter(GroupType, "group");
            var variables = new List<ParameterExpression> { window, groups };
            var steps = new List<Expression>
            {
                Expression.Assign(window, Expression.Subtract(to, from)),
                Expression.Assign(groups, Facets(rows, window)),
            };

            Expression results;
            if (_gap != null)
            {
                var current = Expression.Parameter(RowType, "current");
                var previous = Expression.Parameter(RowType, "previous");
                var index = Expression.Parameter(typeof(int), "index");
                var split = Expression.Lambda(Expression.GreaterThan(Expression.Subtract(Time(current), Time(previous)), Expression.Constant(_gap.Value)), current, previous, index);
                var session = Expression.Parameter(RowsType, "session");
                var first = Linq(nameof(Enumerable.First), RowType, session);
                var last = Linq(nameof(Enumerable.Last), RowType, session);
                var ordered = Linq(nameof(Enumerable.OrderBy), new[] { RowType, typeof(TimeSpan) }, Expression.Field(group, "Item2"), Times());
                var segments = Expression.Call(typeof(MoreEnumerable), nameof(MoreEnumerable.Segment), new[] { RowType }, ordered, split);
                var summaries = Linq(nameof(Enumerable.Select), new[] { RowsType, ResultType }, segments, Expression.Lambda(Summary(Expression.Field(group, "Item1"), session, Expression.Subtract(Time(last), Time(first)), comparison, Expression.Property(Time(first), nameof(TimeSpan.Ticks))), session));
                results = Linq(nameof(Enumerable.SelectMany), new[] { GroupType, ResultType }, groups, Expression.Lambda(summaries, group));
            }
            else if (_bucketTicks == null)
            {
                results = Linq(nameof(Enumerable.Select), new[] { GroupType, ResultType }, groups, Expression.Lambda(Summary(Expression.Field(group, "Item1"), Expression.Field(group, "Item2"), window, comparison, null), group));
            }
            else
            {
                var ticks = _bucketTicks.Value;
                var first = _since?.Ago == true ? 1L : 0L;
                var lookups = Expression.Variable(typeof(List<ILookup<long, (TimeSpan Time, T Item)>>), "lookups");
                var lookup = Expression.Parameter(LookupType, "lookup");
                var row = Expression.Parameter(RowType, "row");
                var bucket = Expression.Parameter(typeof(int), "bucket");
                var index = Expression.Convert(bucket, typeof(long));
                var indexed = Expression.Lambda(Linq(nameof(Enumerable.ToLookup), new[] { RowType, typeof(long) }, Expression.Field(group, "Item2"), Expression.Lambda(Index(Expression.Subtract(Expression.Subtract(Time(row), from), Expression.Constant(TimeSpan.FromTicks(first))), ticks), row)), group);
                var cell = Expression.Lambda(Summary(Expression.Field(group, "Item1"), Expression.Property(lookup, "Item", index), Expression.Constant(TimeSpan.FromTicks(ticks)), comparison, Expression.Add(Expression.Property(from, nameof(TimeSpan.Ticks)), Expression.Multiply(index, Expression.Constant(ticks)))), group, lookup);
                var cells = Expression.Lambda(Expression.Block(Checked(), Linq(nameof(Enumerable.Zip), new[] { GroupType, LookupType, ResultType }, groups, lookups, cell)), bucket);
                var last = Index(Expression.Subtract(window, Expression.Constant(TimeSpan.FromTicks(first + (_until == null || _until.Ago ? 0 : 1)))), ticks);
                var error = Expression.New(typeof(SqlException).GetConstructor(new[] { typeof(string), typeof(SqlErrorKind), typeof(int), typeof(int) }), Expression.Constant(string.Format(CultureInfo.CurrentCulture, "TIMESERIES {0} can't be used because it splits the window into more than {1} buckets.", _buckets.Text, MaxBuckets)), Expression.Constant(SqlErrorKind.Evaluation), Expression.Constant(_buckets.Start), Expression.Constant(_buckets.Text.Length));
                var count = Expression.Condition(Expression.LessThan(last, Expression.Constant(0L)), Expression.Constant(0), Expression.Condition(Expression.LessThan(last, Expression.Constant((long)MaxBuckets)), Expression.Convert(Expression.Increment(last), typeof(int)), Expression.Throw(error, typeof(int))));
                var buckets = Expression.Call(typeof(Enumerable), nameof(Enumerable.Range), null, Expression.Constant(0), count);
                variables.Add(lookups);
                steps.Add(Expression.Assign(lookups, Linq(nameof(Enumerable.ToList), LookupType, Linq(nameof(Enumerable.Select), new[] { GroupType, LookupType }, groups, indexed))));
                results = Linq(nameof(Enumerable.SelectMany), new[] { typeof(int), ResultType }, buckets, cells);
            }

            steps.Add(Linq(nameof(Enumerable.ToList), ResultType, results));

            return Expression.Block(variables, steps);
        }

        private Expression Facets(Expression rows, Expression window)
        {
            var make = GroupType.GetConstructor(new[] { typeof(object[]), RowsType });
            var row = Expression.Parameter(RowType, "row");
            if (_facets.Count == 0)
            {
                var buffered = _aggregates.Count > 1 && _gap == null && _bucketTicks == null ? Expression.Property(Linq(nameof(Enumerable.ToLookup), new[] { RowType, typeof(bool) }, rows, Expression.Lambda(Expression.Constant(true), row)), "Item", Expression.Constant(true)) : rows;

                return Expression.ListInit(Expression.New(GroupsType), Expression.New(make, Expression.Constant(Array.Empty<object>()), buffered));
            }

            var key = Expression.New(typeof(FacetKey).GetConstructor(new[] { typeof(object[]) }), Expression.NewArrayInit(typeof(object), _facets.Select(f => Inline(f.Value, Item(row)))));
            var grouping = Expression.Parameter(typeof(IGrouping<FacetKey, (TimeSpan Time, T Item)>), "grouping");
            var grouped = Linq(nameof(Enumerable.GroupBy), new[] { RowType, typeof(FacetKey) }, rows, Expression.Lambda(key, row));
            var keyed = Expression.Lambda(Expression.New(make, Expression.Property(Expression.Property(grouping, nameof(IGrouping<FacetKey, object>.Key)), nameof(FacetKey.Values)), grouping), grouping);
            var group = Expression.Parameter(GroupType, "group");
            var rank = Expression.Lambda(Expression.Call(typeof(Values), nameof(Values.SortKey), null, _aggregates[0].Aggregate(Expression.Field(group, "Item2"), window)), group);
            Expression ranked = Linq(nameof(Enumerable.OrderByDescending), new[] { GroupType, typeof(object) }, Linq(nameof(Enumerable.Select), new[] { grouping.Type, GroupType }, grouped, keyed), rank);
            if (_limit != null)
            {
                ranked = Linq(nameof(Enumerable.Take), GroupType, ranked, Expression.Constant(_limit.Value));
            }

            return Linq(nameof(Enumerable.ToList), GroupType, ranked);
        }

        private BlockExpression Summary(Expression key, Expression rows, Expression window, Expression comparison, Expression beginTicks)
        {
            var groupRows = Expression.Variable(RowsType, "groupRows");
            var values = new List<(string Name, Expression Value)>();
            if (beginTicks != null)
            {
                var begin = Expression.New(typeof(TimeSpan).GetConstructor(new[] { typeof(long) }), beginTicks);
                values.Add(("BeginTime", begin));
                values.Add(("EndTime", Expression.Add(begin, window)));
            }

            if (_compare != null)
            {
                values.Add(("Comparison", comparison));
            }

            values.AddRange(_facets.Select((f, i) => (f.Name, (Expression)Expression.ArrayIndex(key, Expression.Constant(i)))));
            values.AddRange(_aggregates.Select(a => (a.Name, a.Aggregate(groupRows, window))));

            return Expression.Block(new[] { groupRows }, Expression.Assign(groupRows, rows), Dictionary(values));
        }

        private static BlockExpression Dictionary(List<(string Name, Expression Value)> values)
        {
            var result = Expression.Variable(typeof(Dictionary<string, object>), "result");
            var steps = new List<Expression> { Expression.Assign(result, Expression.New(typeof(Dictionary<string, object>).GetConstructor(new[] { typeof(IEqualityComparer<string>) }), Expression.Constant(StringComparer.OrdinalIgnoreCase))) };
            steps.AddRange(values.Select(v => Expression.Assign(Expression.Property(result, "Item", Expression.Constant(v.Name)), v.Value.Type == typeof(object) ? v.Value : Expression.Convert(v.Value, typeof(object)))));
            steps.Add(Expression.Convert(result, ResultType));

            return Expression.Block(new[] { result }, steps);
        }

        private static BlockExpression Combined(List<Aggregate> inner, Expression<Func<IDictionary<string, double>, object>> outer, Expression rows, Expression window)
        {
            var values = Expression.Variable(typeof(IDictionary<string, double>), "values");
            var results = inner.Select((a, i) => Expression.Variable(typeof(object), "agg" + i.ToString(CultureInfo.InvariantCulture))).ToList();
            var body = Inline(outer, values);
            for (var i = inner.Count - 1; i >= 0; i--)
            {
                var name = Expression.Constant("agg" + i.ToString(CultureInfo.InvariantCulture));
                var known = Expression.Block(Expression.Assign(Expression.Property(values, "Item", name), AggregateFactory<T>.ToDouble(results[i])), body);
                body = Expression.Block(Expression.Assign(results[i], inner[i](rows, window)), Expression.Condition(Expression.Equal(results[i], Expression.Constant(null)), Expression.Constant(null), known));
            }

            var create = Expression.New(typeof(Dictionary<string, double>).GetConstructor(new[] { typeof(IEqualityComparer<string>) }), Expression.Constant(StringComparer.OrdinalIgnoreCase));

            return Expression.Block(results.Prepend(values), Expression.Assign(values, create), body);
        }

        private static Expression Resolve(Bound bound, Expression end)
        {
            return bound.Ago ? Expression.Subtract(end, Expression.Constant(bound.Offset)) : (Expression)Expression.Constant(bound.Offset);
        }

        private static ConditionalExpression Index(Expression time, long ticks)
        {
            var value = Expression.Property(time, nameof(TimeSpan.Ticks));
            var size = Expression.Constant(ticks);
            var index = Expression.Divide(value, size);

            return Expression.Condition(Expression.LessThan(Expression.Modulo(value, size), Expression.Constant(0L)), Expression.Subtract(index, Expression.Constant(1L)), index);
        }

        private MethodCallExpression Checked()
        {
            return Expression.Call(_cancellation, nameof(CancellationToken.ThrowIfCancellationRequested), null);
        }

        private static LambdaExpression Times()
        {
            var row = Expression.Parameter(RowType, "row");

            return Expression.Lambda(Time(row), row);
        }

        private static MemberExpression Time(Expression row)
        {
            return Expression.Field(row, "Item1");
        }

        private static MemberExpression Item(Expression row)
        {
            return Expression.Field(row, "Item2");
        }

        private static MethodCallExpression Linq(string method, Type type, params Expression[] arguments)
        {
            return Linq(method, new[] { type }, arguments);
        }

        private static MethodCallExpression Linq(string method, Type[] types, params Expression[] arguments)
        {
            return Expression.Call(typeof(Enumerable), method, types, arguments);
        }

        private sealed class Substitution : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;
            private readonly Expression _argument;

            internal Substitution(ParameterExpression parameter, Expression argument)
            {
                _parameter = parameter;
                _argument = argument;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _parameter ? _argument : base.VisitParameter(node);
            }
        }
    }
}
