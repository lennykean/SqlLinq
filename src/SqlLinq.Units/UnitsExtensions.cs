using System;
using System.Collections.Generic;

namespace SqlLinq.Units
{
    public static class UnitsExtensions
    {
        public static IEnumerable<TSource> UnitQuery<TSource>(
            this IEnumerable<TSource> source,
            string sql,
            UnitConversions units,
            Func<string, string> rewrite = null)
        {
            return source.UnitQuery<TSource, TSource>(sql, units, rewrite);
        }

        public static IEnumerable<TResult> UnitQuery<TSource, TResult>(
            this IEnumerable<TSource> source,
            string sql,
            UnitConversions units,
            Func<string, string> rewrite = null)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            return source.Query<TSource, TResult>(rewrite == null ? sql : rewrite(sql), new ISqlFunction[] { units });
        }
    }
}
