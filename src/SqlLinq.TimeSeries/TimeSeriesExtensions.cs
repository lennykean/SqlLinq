using System;
using System.Collections.Generic;
using System.Threading;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public static class TimeSeriesExtensions
    {
        public static IList<IDictionary<string, object>> TimeSeriesQuery<T>(
            this IEnumerable<T> source,
            string sql,
            Func<T, TimeSpan> time,
            Func<ExpressionNode, ExpressionNode> rewrite = null)
        {
            return new TimeSeriesQuery<T>(sql, time, rewrite).Evaluate(source);
        }

        public static IList<IDictionary<string, object>> TimeSeriesQuery<T>(
            this IEnumerable<T> source,
            string sql,
            Func<T, TimeSpan> time,
            Func<ExpressionNode, ExpressionNode> rewrite,
            IEnumerable<ISqlFunction> functions,
            CancellationToken cancellationToken = default)
        {
            return new TimeSeriesQuery<T>(sql, time, rewrite, functions).Evaluate(source, cancellationToken);
        }
    }
}
