using System;
using System.Collections.Generic;
using System.Threading;

namespace SqlLinq.TimeSeries
{
    public static class TimeSeriesExtensions
    {
        public static IList<IDictionary<string, object>> TimeSeriesQuery<T>(
            this IEnumerable<T> source,
            string sql,
            Func<T, TimeSpan> time,
            Func<string, string> rewrite = null)
        {
            return new TimeSeriesQuery<T>(sql, time, rewrite).Evaluate(source);
        }

        public static IList<IDictionary<string, object>> TimeSeriesQuery<T>(
            this IEnumerable<T> source,
            string sql,
            Func<T, TimeSpan> time,
            Func<string, string> rewrite,
            IEnumerable<ISqlFunction> functions,
            CancellationToken cancellationToken = default)
        {
            return new TimeSeriesQuery<T>(sql, time, rewrite, functions).Evaluate(source, cancellationToken);
        }
    }
}
