using System;
using System.Collections.Generic;

namespace SqlLinq.Units
{
    public static class UnitsExtensions
    {
        public static IEnumerable<TSource> UnitQuery<TSource>(
            this IEnumerable<TSource> source,
            string sql,
            UnitConversions units)
        {
            return source.UnitQuery<TSource, TSource>(sql, units);
        }

        public static IEnumerable<TResult> UnitQuery<TSource, TResult>(
            this IEnumerable<TSource> source,
            string sql,
            UnitConversions units)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            return source.Query<TSource, TResult>(sql, new ISqlFunction[] { units });
        }
    }
}
