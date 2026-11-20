using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SqlLinq.TimeSeries
{
    static class Values
    {
        public static readonly IComparer<object> Comparer = new ValueComparer();

        public static double ToDouble(object value)
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        public static double ParsePercentile(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) || p < 0 || p > 100)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be used as a percentile because it isn't between 0 and 100.", text), SqlErrorKind.Semantic);
            }

            return p;
        }

        public static List<(TimeSpan Time, T Item)> Read<T>(IEnumerable<T> source, Func<T, TimeSpan> time, CancellationToken cancellationToken)
        {
            return source
                .Select(item =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return (Time: time(item), Item: item);
                })
                .ToList();
        }

        public static object SortKey(object value)
        {
            return IsNumeric(value) ? ToDouble(value) : value;
        }

        public static bool IsNumeric(object value)
        {
            var code = Convert.GetTypeCode(value);

            return code >= TypeCode.SByte && code <= TypeCode.Decimal;
        }
    }
}
