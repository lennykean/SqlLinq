using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public static class FacetExpression
    {
        internal static readonly HashSet<string> BucketNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "buckets", "cases" };
        private static readonly Regex Alias = new Regex(@"^(.+)\s+AS\s+(\[[^\]]+\]|\w+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex Case = new Regex(@"^(.+)\s+AS\s+'((?:''|[^'])*)'\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static string Name(string expression)
        {
            var alias = Alias.Match(expression);

            return SqlSyntax.Unbracket(alias.Success ? alias.Groups[2].Value : expression);
        }

        public static IReadOnlyList<string> Labels(string expression)
        {
            var facet = Read(expression, 0, expression, null);
            if (facet.Labels == null && facet.Error != null && !facet.Plain)
            {
                throw facet.Error;
            }

            return facet.Labels;
        }

        public static Func<T, object> Compile<T>(string expression, Func<ExpressionNode, ExpressionNode> rewrite = null)
        {
            return Compile<T>(expression, rewrite, null);
        }

        public static Func<T, object> Compile<T>(string expression, Func<ExpressionNode, ExpressionNode> rewrite, IEnumerable<ISqlFunction> functions)
        {
            var facet = Read(expression, 0, expression, functions);
            if (facet.Error != null)
            {
                throw facet.Error;
            }

            return Lambda<T>(TimeSeriesSyntaxTree.Rewritten(facet, e => rewrite?.Invoke(e) ?? e)).Compile();
        }

        internal static TimeSeriesExpression Read(string text, int start, string facet, IEnumerable<ISqlFunction> functions)
        {
            var alias = Alias.Match(facet);
            var expression = alias.Success ? alias.Groups[1].Value.TrimEnd() : facet;
            var syntax = SqlSyntaxTree.ParseExpression(text, start, expression.Length, TimeSeriesFunction.With(BucketNames, functions));
            var item = new TimeSeriesExpression(expression, alias.Success ? SqlSyntax.Unbracket(alias.Groups[2].Value) : null, Name(facet), start, syntax);
            if (syntax.Root == null)
            {
                var error = TimeSeriesSyntaxTree.Unbalanced(syntax.Error, expression);
                var unclosed = !ReferenceEquals(error, syntax.Error) && BucketNames.Any(n => string.Compare(text, error.Start, n, 0, n.Length, StringComparison.OrdinalIgnoreCase) == 0);
                item.Plain = !TimeSeriesFunction.CallsAny(expression, BucketNames);
                item.Error = item.Plain || unclosed ? error : new SqlException("FACET buckets() or cases() must be a complete facet expression.", start, expression.Length);

                return item;
            }

            var calls = TimeSeriesFunction.Calls((ExpressionNode)syntax.Root, BucketNames);
            if (calls.Count == 0)
            {
                item.Plain = true;
                item.Error = syntax.Error;

                return item;
            }

            if (calls.Count != 1 || calls[0] != syntax.Root || expression.Trim().Length != expression.Length)
            {
                item.Error = new SqlException("FACET buckets() or cases() must be a complete facet expression.", start, expression.Length);

                return item;
            }

            var arguments = calls[0].Arguments;
            var args = arguments.Count == 0 ? new List<string> { string.Empty } : arguments.Select(a => a.Text).ToList();
            if (calls[0].FunctionName.Equals("cases", StringComparison.OrdinalIgnoreCase))
            {
                var cases = args.Select(a => Case.Match(a)).ToList();
                if (cases.Any(c => !c.Success))
                {
                    item.Error = new SqlException("cases takes conditions with labels, e.g. cases(RPM < 2000 AS 'Low', RPM >= 2000 AS 'High').", start, expression.Length);

                    return item;
                }

                item.Labels = cases.Select(c => c.Groups[2].Value.Replace("''", "'")).Distinct().ToArray();
                item.CaseLabels = cases.Select(c => c.Groups[2].Value.Replace("''", "'")).ToArray();
                item.CaseTrees = arguments.Select((a, i) => SqlSyntaxTree.ParseExpression(text, a.Start, cases[i].Groups[1].Length, functions)).ToList();
                item.Conditions = item.CaseTrees.Select(t => (ExpressionNode)t.Root).ToList();
                item.Error = item.CaseTrees.Select(t => t.Error).FirstOrDefault(e => e != null);

                return item;
            }

            if (args.Count != 3 ||
                !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ceiling) ||
                !double.IsFinite(ceiling) ||
                ceiling <= 0 ||
                !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                count < 1 ||
                count > 1000 ||
                ceiling / count <= 0)
            {
                item.Error = new SqlException("buckets takes (numeric expression, positive finite ceiling, bucket count from 1 to 1000).", start, expression.Length);

                return item;
            }

            var boundaries = new double[count - 1];
            var decimalWidth = ceiling < (double)decimal.MaxValue ? (decimal)ceiling / count : 0;
            var useDecimal = decimalWidth > 0 && (double)decimalWidth == ceiling / count;

            for (var i = 0; i < boundaries.Length; i++)
            {
                boundaries[i] = useDecimal ? (double)(decimalWidth * (i + 1)) : ceiling * ((i + 1d) / count);
            }

            var bucketLabels = new string[count];

            for (var i = 0; i < count; i++)
            {
                if (count == 1)
                {
                    bucketLabels[i] = "All values";
                }
                else if (i == 0)
                {
                    bucketLabels[i] = "< " + Format(boundaries[0]);
                }
                else if (i == count - 1)
                {
                    bucketLabels[i] = ">= " + Format(boundaries[i - 1]);
                }
                else
                {
                    bucketLabels[i] = Format(boundaries[i - 1]) + " - < " + Format(boundaries[i]);
                }
            }

            item.Labels = bucketLabels;
            item.Boundaries = boundaries;
            item.Error = TimeSeriesSyntaxTree.Value(text, arguments.Count == 0 ? null : arguments[0], functions);

            return item;
        }

        internal static Expression<Func<T, object>> Lambda<T>(TimeSeriesExpression facet)
        {
            if (facet.Plain)
            {
                return SqlExpression.Lambda<T>(facet.Expression);
            }

            var row = Expression.Parameter(typeof(T), "row");
            if (facet.Boundaries == null)
            {
                var predicates = facet.Conditions.Select(SqlExpression.Lambda<T>).ToList();
                Expression cases = Expression.Constant(null);
                for (var i = predicates.Count - 1; i >= 0; i--)
                {
                    cases = Expression.Condition(TimeSeriesQueryTree<T>.IsTrue(TimeSeriesQueryTree<T>.Inline(predicates[i], row)), Expression.Constant(facet.CaseLabels[i], typeof(object)), cases);
                }

                return Expression.Lambda<Func<T, object>>(cases, row);
            }

            var value = SqlExpression.Lambda<T>(((NodeWithId)facet.Expression).Arguments[0].Expression);
            var bucket = Expression.Call(typeof(FacetExpression), nameof(Bucket), null, TimeSeriesQueryTree<T>.Inline(value, row), Expression.Constant(facet.Boundaries), Expression.Constant(facet.Labels.ToArray()));

            return Expression.Lambda<Func<T, object>>(bucket, row);
        }

        internal static object Bucket(object value, double[] boundaries, string[] labels)
        {
            if (value == null)
            {
                return null;
            }

            var code = Type.GetTypeCode(value.GetType());
            if (value.GetType().IsEnum || code < TypeCode.SByte || code > TypeCode.Decimal)
            {
                throw new SqlException("buckets requires numeric values; received " + value.GetType().Name + ".");
            }

            var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (!double.IsFinite(number))
            {
                return null;
            }

            var index = Array.BinarySearch(boundaries, number);

            return labels[index < 0 ? ~index : index + 1];
        }

        private static string Format(double number)
        {
            return number.ToString("G", CultureInfo.InvariantCulture);
        }
    }
}
