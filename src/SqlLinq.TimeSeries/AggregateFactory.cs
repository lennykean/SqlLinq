using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LinqStatistics;
using MathNet.Numerics.Statistics;
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

        private static readonly HashSet<string> Numeric = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sum", "average", "avg", "stddev", "median", "percentile", "derivative" };

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
                    return Over(name, args, first, values => Boxed(Call(nameof(Enumerable.Count), Element(values), values)));
                case "uniquecount":
                    return Over(name, args, first, values => Boxed(Call(nameof(Enumerable.Count), Element(values), Call(nameof(Enumerable.Distinct), Element(values), values))));
                case "sum":
                    return Over(name, args, first, values => Boxed(Call(nameof(Enumerable.Sum), Element(values), values, Doubles(Element(values)))));
                case "average":
                case "avg":
                    return Over(name, args, first, values => UnlessEmpty(values, Boxed(Call(nameof(Enumerable.Average), Element(values), values, Doubles(Element(values))))));
                case "min":
                    return Over(name, args, first, values => UnlessEmpty(values, Element(values) == typeof(object) ? Call(nameof(Enumerable.Aggregate), typeof(object), values, Best(Expression.LessThan)) : (Expression)Boxed(Call(nameof(Enumerable.Min), Element(values), values))));
                case "max":
                    return Over(name, args, first, values => UnlessEmpty(values, Element(values) == typeof(object) ? Call(nameof(Enumerable.Aggregate), typeof(object), values, Best(Expression.GreaterThan)) : (Expression)Boxed(Call(nameof(Enumerable.Max), Element(values), values))));
                case "stddev":
                    return Over(name, args, first, values => Expression.Condition(Expression.Not(Call(nameof(Enumerable.Any), Element(values), Call(nameof(Enumerable.Skip), Element(values), values, Expression.Constant(1)))), Expression.Constant(null), Boxed(Expression.Call(typeof(EnumerableStats), nameof(EnumerableStats.StandardDeviation), null, Call(nameof(Enumerable.Select), new[] { Element(values), typeof(double) }, values, Doubles(Element(values)))))));
                case "median":
                    return Over(name, args, first, values => Percentile(values, 50d));
                case "percentile":
                    if (args.Count != 2)
                    {
                        throw new SqlException("percentile can't be calculated because here it takes 2 arguments, not " + arguments.Count + ".", SqlErrorKind.Semantic);
                    }
                    var percentile = Values.ParsePercentile(args[1]);

                    return Over(name, args.Take(1).ToList(), first, values => Percentile(values, percentile));
                case "latest":
                    return ByTime(name, args, first, Expression.GreaterThanOrEqual);
                case "earliest":
                    return ByTime(name, args, first, Expression.LessThan);
                case "rate":
                    return Rate(args, arguments);
                case "derivative":
                    return Derivative(args, arguments);
            }
            throw new SqlException(name + " can't be calculated because it isn't an aggregate function.", SqlErrorKind.UnknownName);
        }

        private Aggregate Over(string name, List<string> args, ArgumentNode first, Func<Expression, Expression> aggregate)
        {
            if (args.Count != 1 || args[0].Length == 0)
            {
                throw new SqlException(name + " can't be calculated because it takes 1 argument, not " + (first == null ? 0 : args.Count) + ".", SqlErrorKind.Semantic);
            }

            var value = Numbers(name, first, _compile(first));
            return (rows, window) =>
            {
                var typed = value.Body is UnaryExpression boxed && boxed.NodeType == ExpressionType.Convert ? boxed.Operand : value.Body;
                var element = Nullable.GetUnderlyingType(typed.Type) ?? typed.Type;
                if (element.IsEnum || Type.GetTypeCode(element) < TypeCode.SByte || Type.GetTypeCode(element) > TypeCode.Decimal)
                {
                    typed = value.Body;
                    element = typeof(object);
                }

                var values = Expression.Variable(typeof(IEnumerable<>).MakeGenericType(element), "values");
                var row = Expression.Parameter(Row, "row");
                var selected = Call(nameof(Enumerable.Select), new[] { Row, typed.Type }, rows, Expression.Lambda(TimeSeriesQueryTree<T>.Inline(Expression.Lambda(typed, value.Parameters), Expression.Field(row, "Item2")), row));
                var present = element == typeof(object) ? Call(nameof(Enumerable.OfType), typeof(object), selected) : selected;
                if (typed.Type != element)
                {
                    var nullable = Expression.Parameter(typed.Type, "value");
                    var known = Call(nameof(Enumerable.Where), typed.Type, selected, Expression.Lambda(Expression.NotEqual(nullable, Expression.Constant(null, typed.Type)), nullable));
                    present = Call(nameof(Enumerable.Select), new[] { typed.Type, element }, known, Expression.Lambda(Expression.Property(nullable, nameof(Nullable<int>.Value)), nullable));
                }

                return Expression.Block(new[] { values }, Expression.Assign(values, present), aggregate(values));
            };
        }

        private Aggregate ByTime(string name, List<string> args, ArgumentNode first, Func<Expression, Expression, BinaryExpression> better)
        {
            if (args.Count != 1)
            {
                throw new SqlException(name + " can't be calculated because it takes 1 argument, not " + args.Count + ".", SqlErrorKind.Semantic);
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
                throw new SqlException(args.Count != 2 ? "rate can't be calculated because it takes 2 arguments, not " + arguments.Count + "." : "rate can't be calculated because its first argument, " + args[0] + ", isn't an aggregate.", SqlErrorKind.Semantic);
            }

            var inner = Create(call);

            var per = Bound.Parse(args[1], "rate", arguments[1].Start).Offset.Ticks;
            if (per <= 0)
            {
                throw new SqlException("rate can't be calculated because its second argument, " + args[1] + ", isn't a positive duration.", SqlErrorKind.Semantic);
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
                throw new SqlException(args.Count != 2 ? "derivative can't be calculated because it takes 2 arguments, not " + arguments.Count + "." : "derivative can't be calculated because its first argument is empty.", SqlErrorKind.Semantic);
            }

            var per = Bound.Parse(args[1], "derivative", arguments[1].Start);
            if (per.Ago || per.Offset <= TimeSpan.Zero)
            {
                throw new SqlException("derivative can't be calculated because its second argument, " + args[1] + ", isn't a positive duration.", SqlErrorKind.Semantic);
            }

            var value = Numbers("derivative", arguments[0], _compile(arguments[0]));
            var seconds = per.Offset.TotalSeconds;

            return (rows, window) =>
            {
                var points = Expression.Variable(typeof(List<(TimeSpan Time, object Value)>), "points");
                var point = Expression.Parameter(Point, "point");
                var start = Expression.Field(Expression.Property(points, "Item", Expression.Constant(0)), "Item1");
                var xy = Expression.Call(typeof(Tuple), nameof(Tuple.Create), new[] { typeof(double), typeof(double) }, Expression.Property(Expression.Subtract(Expression.Field(point, "Item1"), start), nameof(TimeSpan.TotalSeconds)), ToDouble(Expression.Field(point, "Item2")));
                var fit = Expression.Call(typeof(EnumerableStats), nameof(EnumerableStats.LeastSquares), null, Call(nameof(Enumerable.Select), new[] { Point, xy.Type }, points, Expression.Lambda(xy, point)));
                var single = Call(nameof(Enumerable.All), Point, points, Expression.Lambda(Expression.Equal(Expression.Field(point, "Item1"), start), point));

                return Expression.Block(new[] { points }, Expression.Assign(points, Call(nameof(Enumerable.ToList), Point, Points(rows, value))), Expression.Condition(single, Expression.Constant(null), Boxed(Expression.Multiply(Expression.Property(fit, nameof(LeastSquares.M)), Expression.Constant(seconds)))));
            };
        }

        private static ConditionalExpression Percentile(Expression values, double p)
        {
            var value = Expression.Parameter(typeof(double), "value");
            var sorted = Call(nameof(Enumerable.OrderBy), new[] { typeof(double), typeof(double) }, Call(nameof(Enumerable.Select), new[] { Element(values), typeof(double) }, values, Doubles(Element(values))), Expression.Lambda(value, value));

            return UnlessEmpty(values, Boxed(Expression.Call(typeof(SortedArrayStatistics), nameof(SortedArrayStatistics.QuantileCustom), null, Call(nameof(Enumerable.ToArray), typeof(double), sorted), Expression.Constant(p / 100), Expression.Constant(QuantileDefinition.R7))));
        }

        private static Expression<Func<T, object>> Numbers(string name, ArgumentNode argument, Expression<Func<T, object>> value)
        {
            var type = value?.Body is UnaryExpression boxed && boxed.NodeType == ExpressionType.Convert ? boxed.Operand.Type : typeof(object);
            type = Nullable.GetUnderlyingType(type) ?? type;
            var code = Type.GetTypeCode(type);
            if (Numeric.Contains(name) && type != typeof(object) && !type.IsAbstract && code != TypeCode.Boolean && code != TypeCode.String && (code < TypeCode.SByte || code > TypeCode.Decimal))
            {
                throw new SqlException("Arithmetic can't use " + type.Name + " values because they aren't numbers.", SqlErrorKind.Semantic, argument.Start, argument.Length);
            }

            return value;
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

        private static LambdaExpression Doubles(Type type)
        {
            var value = Expression.Parameter(type, "value");

            return Expression.Lambda(type == typeof(object) ? ToDouble(value) : Expression.Convert(value, typeof(double)), value);
        }

        private static Type Element(Expression values)
        {
            return values.Type.GetGenericArguments()[0];
        }

        internal static Expression ToDouble(Expression value)
        {
            return Expression.Call(typeof(Values), nameof(Values.ToDouble), null, value);
        }

        private static ConditionalExpression UnlessEmpty(Expression values, Expression value)
        {
            return Expression.Condition(Expression.Not(Call(nameof(Enumerable.Any), Element(values), values)), Expression.Constant(null), value);
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
