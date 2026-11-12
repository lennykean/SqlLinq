using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    sealed class AggregateFactory<T>
    {
        public static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "count",
            "sum",
            "average",
            "avg",
            "min",
            "max",
            "stddev",
            "uniqueCount",
            "percentile",
            "median",
            "latest",
            "earliest",
            "rate",
            "derivative"
        };

        private static readonly Type Row = typeof((TimeSpan Time, T Item));

        private static readonly Type Point = typeof((TimeSpan Time, object Value));

        private readonly Func<ArgumentNode, Expression<Func<T, object>>> _compile;

        public AggregateFactory(Func<ArgumentNode, Expression<Func<T, object>>> compile)
        {
            _compile = compile;
        }

        public Aggregate Create(NodeWithId call)
        {
            try
            {
                return Create(call.FunctionName, call.Arguments);
            }
            catch (SqlException e) when (e.Start < 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, call.Start, call.Length);
            }
        }

        public Aggregate Create(string name, IReadOnlyList<ArgumentNode> arguments)
        {
            var args = arguments.Count == 0 ? new List<string> { string.Empty } : arguments.Select(a => a.Text).ToList();
            var first = arguments.Count == 0 ? null : arguments[0];

            switch (name.ToLowerInvariant())
            {
                case "count":
                    if (args.Count == 1 && args[0] == "*")
                    {
                        return (rows, window) => Boxed(Call(nameof(Enumerable.Count), Row, rows));
                    }
                    return Over(name, args, first, values => Boxed(Call(nameof(Enumerable.Count), typeof(object), values)));
                case "uniquecount":
                    return Over(name, args, first, values => Boxed(Call(nameof(Enumerable.Count), typeof(object), Call(nameof(Enumerable.Distinct), typeof(object), values))));
                case "sum":
                    return Over(name, args, first, values => Boxed(Call(nameof(Enumerable.Sum), typeof(object), values, Doubles())));
                case "average":
                case "avg":
                    return Over(name, args, first, values => UnlessEmpty(values, Boxed(Call(nameof(Enumerable.Average), typeof(object), values, Doubles()))));
                case "min":
                    return Over(name, args, first, values => UnlessEmpty(values, Call(nameof(Enumerable.Aggregate), typeof(object), values, Best(Expression.LessThan))));
                case "max":
                    return Over(name, args, first, values => UnlessEmpty(values, Call(nameof(Enumerable.Aggregate), typeof(object), values, Best(Expression.GreaterThan))));
                case "stddev":
                    return Over(name, args, first, values => Expression.Call(typeof(Values), nameof(Values.SampleStandardDeviation), null, Call(nameof(Enumerable.ToList), typeof(double), Call(nameof(Enumerable.Select), new[] { typeof(object), typeof(double) }, values, Doubles()))));
                case "median":
                    return Over(name, args, first, values => Expression.Call(typeof(Values), nameof(Values.Percentile), null, values, Expression.Constant(50d)));
                case "percentile":
                    if (args.Count != 2)
                    {
                        throw new SqlException("percentile takes (value, percentile) here, e.g. percentile(RPM, 95).", SqlErrorKind.Semantic);
                    }
                    var percentile = Values.ParsePercentile(args[1]);

                    return Over(name, args.Take(1).ToList(), first, values => Expression.Call(typeof(Values), nameof(Values.Percentile), null, values, Expression.Constant(percentile)));
                case "latest":
                    return ByTime(name, args, first, Expression.GreaterThanOrEqual);
                case "earliest":
                    return ByTime(name, args, first, Expression.LessThan);
                case "rate":
                    return Rate(args, arguments);
                case "derivative":
                    return Derivative(args, arguments);
            }
            throw new SqlException(name + " isn't an aggregate function.", SqlErrorKind.UnknownName);
        }

        private Aggregate Over(string name, List<string> args, ArgumentNode first, Func<Expression, Expression> aggregate)
        {
            if (args.Count != 1 || args[0].Length == 0)
            {
                throw new SqlException(name + " takes one value, e.g. " + name + "(RPM).", SqlErrorKind.Semantic);
            }

            var value = _compile(first);
            return (rows, window) =>
            {
                var values = Expression.Variable(typeof(IEnumerable<object>), "values");
                var row = Expression.Parameter(Row, "row");
                var selected = Call(nameof(Enumerable.Select), new[] { Row, typeof(object) }, rows, Expression.Lambda(TimeSeriesQueryTree<T>.Inline(value, Expression.Field(row, "Item2")), row));
                var present = Call(nameof(Enumerable.OfType), typeof(object), selected);

                return Expression.Block(new[] { values }, Expression.Assign(values, present), aggregate(values));
            };
        }

        private Aggregate ByTime(string name, List<string> args, ArgumentNode first, Func<Expression, Expression, BinaryExpression> better)
        {
            if (args.Count != 1)
            {
                throw new SqlException(name + " takes one value, e.g. " + name + "(ECT).", SqlErrorKind.Semantic);
            }

            var value = _compile(first);
            return (rows, window) =>
            {
                var best = Expression.Parameter(Point, "best");
                var point = Expression.Parameter(Point, "point");
                var pick = Expression.Condition(
                    Expression.OrElse(Expression.Equal(Expression.Field(best, "Item2"), Expression.Constant(null)), better(Expression.Field(point, "Item1"), Expression.Field(best, "Item1"))),
                    point,
                    best);
                var latest = Expression.Call(typeof(Enumerable), nameof(Enumerable.Aggregate), new[] { Point, Point }, Points(rows, value), Expression.Constant(default((TimeSpan, object))), Expression.Lambda(pick, best, point));

                return Expression.Field(latest, "Item2");
            };
        }

        private Aggregate Rate(List<string> args, IReadOnlyList<ArgumentNode> arguments)
        {
            var call = args.Count == 2 ? arguments[0].Expression as NodeWithId : null;
            if (call == null || !(call.Function is TimeSeriesFunction) || !Names.Contains(call.FunctionName))
            {
                throw new SqlException("rate takes (aggregate, duration), e.g. rate(count(*), 1 second).", SqlErrorKind.Semantic);
            }

            var inner = Create(call);

            var per = Bound.Parse(args[1], "rate", arguments[1].Start).Offset.Ticks;
            if (per <= 0)
            {
                throw new SqlException("rate needs a positive duration, e.g. rate(count(*), 1 second).", SqlErrorKind.Semantic);
            }

            return (rows, window) =>
            {
                var value = Expression.Variable(typeof(object), "value");
                var scaled = Expression.Divide(Expression.Multiply(ToDouble(value), Expression.Constant((double)per)), Expression.Convert(Expression.Property(window, nameof(TimeSpan.Ticks)), typeof(double)));
                var unknown = Expression.OrElse(Expression.Equal(value, Expression.Constant(null)), Expression.LessThanOrEqual(window, Expression.Constant(TimeSpan.Zero)));

                return Expression.Block(new[] { value }, Expression.Assign(value, inner(rows, window)), Expression.Condition(unknown, Expression.Constant(null), Boxed(scaled)));
            };
        }

        private Aggregate Derivative(List<string> args, IReadOnlyList<ArgumentNode> arguments)
        {
            if (args.Count != 2 || args[0].Length == 0)
            {
                throw new SqlException("derivative takes (value, duration), e.g. derivative(RPM, 1 second).", SqlErrorKind.Semantic);
            }

            var per = Bound.Parse(args[1], "derivative", arguments[1].Start);
            if (per.Ago || per.Offset <= TimeSpan.Zero)
            {
                throw new SqlException("derivative needs a positive duration, e.g. derivative(RPM, 1 second).", SqlErrorKind.Semantic);
            }

            var value = _compile(arguments[0]);
            var seconds = per.Offset.TotalSeconds;

            return (rows, window) => Expression.Call(typeof(Values), nameof(Values.Derivative), null, Call(nameof(Enumerable.ToList), Point, Points(rows, value)), Expression.Constant(seconds));
        }

        private static MethodCallExpression Points(Expression rows, LambdaExpression value)
        {
            var row = Expression.Parameter(Row, "row");
            var point = Expression.New(Point.GetConstructor(new[] { typeof(TimeSpan), typeof(object) }), Expression.Field(row, "Item1"), TimeSeriesQueryTree<T>.Inline(value, Expression.Field(row, "Item2")));
            var selected = Call(nameof(Enumerable.Select), new[] { Row, Point }, rows, Expression.Lambda(point, row));
            var present = Expression.Parameter(Point, "point");

            return Call(nameof(Enumerable.Where), Point, selected, Expression.Lambda(Expression.NotEqual(Expression.Field(present, "Item2"), Expression.Constant(null)), present));
        }

        private static LambdaExpression Best(Func<Expression, Expression, BinaryExpression> keep)
        {
            var best = Expression.Parameter(typeof(object), "best");
            var value = Expression.Parameter(typeof(object), "value");
            var compare = Expression.Call(Expression.Field(null, typeof(Values), nameof(Values.Comparer)), nameof(IComparer<object>.Compare), null, value, best);

            return Expression.Lambda(Expression.Condition(keep(compare, Expression.Constant(0)), value, best), best, value);
        }

        private static LambdaExpression Doubles()
        {
            var value = Expression.Parameter(typeof(object), "value");

            return Expression.Lambda(ToDouble(value), value);
        }

        internal static Expression ToDouble(Expression value)
        {
            return Expression.Call(typeof(Values), nameof(Values.ToDouble), null, value);
        }

        private static ConditionalExpression UnlessEmpty(Expression values, Expression value)
        {
            return Expression.Condition(Expression.Not(Call(nameof(Enumerable.Any), typeof(object), values)), Expression.Constant(null), value);
        }

        private static UnaryExpression Boxed(Expression value)
        {
            return Expression.Convert(value, typeof(object));
        }

        private static MethodCallExpression Call(string method, Type type, params Expression[] arguments)
        {
            return Call(method, new[] { type }, arguments);
        }

        private static MethodCallExpression Call(string method, Type[] types, params Expression[] arguments)
        {
            return Expression.Call(typeof(Enumerable), method, types, arguments);
        }
    }
}
