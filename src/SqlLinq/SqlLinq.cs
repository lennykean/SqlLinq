using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SqlLinq
{
    public static class SqlLinq
    {
        public static TSource QueryScalar<TSource>(this IEnumerable<TSource> enumerable, string sql)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(string.IsNullOrEmpty(sql) == false, "Sql cannot be empty");

            return enumerable.QueryScalar<TSource, TSource>(sql);
        }

        public static TResult QueryScalar<TSource, TResult>(this IEnumerable<TSource> enumerable, string sql)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(string.IsNullOrEmpty(sql) == false, "Sql cannot be empty");

            ScalarQuery<TSource, TResult> query = new ScalarQuery<TSource, TResult>(sql);
            query.Compile();
            return enumerable.QueryScalar<TSource, TResult>(query);
        }

        public static TResult QueryScalar<TSource, TResult>(this IEnumerable<TSource> enumerable, ScalarQuery<TSource, TResult> query)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(query != null);

            return query.Evaluate(enumerable);
        }

        public static IEnumerable<TSource> Query<TSource>(this IEnumerable<TSource> enumerable, string sql)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(string.IsNullOrEmpty(sql) == false, "Sql cannot be empty");

            return enumerable.Query<TSource, TSource>(sql);
        }

        public static IEnumerable<TResult> Query<TSource, TResult>(this IEnumerable<TSource> enumerable, string sql)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(string.IsNullOrEmpty(sql) == false, "Sql cannot be empty");

            return SourcesExtensions.Run<TSource, TResult>(new Dictionary<string, IEnumerable> { ["this"] = enumerable }, sql, null, false);
        }

        public static IEnumerable<TSource> Query<TSource>(this IEnumerable<TSource> enumerable, string sql, CancellationToken cancellationToken)
        {
            return enumerable.Query<TSource, TSource>(sql, cancellationToken);
        }

        public static IEnumerable<TResult> Query<TSource, TResult>(this IEnumerable<TSource> enumerable, string sql, CancellationToken cancellationToken)
        {
            return SourcesExtensions.Run<TSource, TResult>(new Dictionary<string, IEnumerable> { ["this"] = enumerable }, sql, null, false, cancellationToken: cancellationToken);
        }

        public static IEnumerable<TResult> Query<TSource, TResult>(this IEnumerable<TSource> enumerable, Query<TSource, TResult> query)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(query != null);

            return query.Evaluate(enumerable);
        }

        public static IEnumerable<TSource> Query<TSource>(
            this IEnumerable<TSource> enumerable,
            string sql,
            IEnumerable<ISqlFunction> functions,
            CancellationToken cancellationToken = default)
        {
            return enumerable.Query<TSource, TSource>(sql, functions, cancellationToken);
        }

        public static IEnumerable<TResult> Query<TSource, TResult>(
            this IEnumerable<TSource> enumerable,
            string sql,
            IEnumerable<ISqlFunction> functions,
            CancellationToken cancellationToken = default)
        {
            return SourcesExtensions.Run<TSource, TResult>(new Dictionary<string, IEnumerable> { ["this"] = enumerable }, sql, functions, false, cancellationToken: cancellationToken);
        }

        public static TResult QueryScalar<TSource, TInner, TResult>(this IEnumerable<TSource> enumerable, string sql, IEnumerable<TInner> inner)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(inner != null);
            Debug.Assert(string.IsNullOrEmpty(sql) == false, "Sql cannot be empty");

            ScalarJoinQuery<TSource, TInner, TResult> query = new ScalarJoinQuery<TSource, TInner, TResult>(sql);
            query.Compile();
            return query.Evaluate(enumerable, inner);
        }

        public static IEnumerable<TResult> Query<TSource, TInner, TResult>(this IEnumerable<TSource> enumerable, string sql, IEnumerable<TInner> inner)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(inner != null);
            Debug.Assert(string.IsNullOrEmpty(sql) == false, "Sql cannot be empty");

            return SourcesExtensions.Run<TSource, TResult>(new Dictionary<string, IEnumerable> { ["this"] = enumerable, ["that"] = inner }, sql, null, true);
        }
    }
}
