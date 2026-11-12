using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Globalization;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree
{
    [SyntaxNode(RuleConstants.RULE_COLUMNS)]
    public class Columns : NonTerminalNode
    {
        public Columns()
        {
        }

        public bool Distinct
        {
            get
            {
                return FindChild<NonTerminalNode>(RuleConstants.RULE_RESTRICTION_DISTINCT) != null;
            }
        }

        public bool IsAggregateOnlyQuery
        {
            get
            {   // we have only calculated fields
                var columns = ColumnSources.ToList();

                return columns.Any(c => c is AggregateNode || c is ComputedColumn computed && computed.HasAggregates)
                    && columns.All(c => c is AggregateNode || c is ComputedColumn computed && !computed.Fields.Any());
            }
        }

        public IEnumerable<AggregateNode> Aggregates
        {
            get
            {
                return FindDescendants<AggregateNode>().Where(a => a.EnclosingSelect() == EnclosingSelect());
            }
        }

        public Func<IEnumerable<TSource>, TResult> CreateScalarSelector<TSource, TResult>(Func<IEnumerable<TSource>, IEnumerable<object>> selector)
        {
            ParameterExpression source = Expression.Parameter(typeof(IEnumerable<TSource>), "source");
            var eval = Expression.Call(Expression.Constant(selector.Target), selector.Method, source);
            var _get = Expression.Call(GetType(), "GetScalarValue", new Type[] { typeof(TResult) }, eval);

            return Expression.Lambda<Func<IEnumerable<TSource>, TResult>>(_get, source).Compile();
        }

        public Func<IEnumerable<TSource>, IEnumerable<TInner>, TResult> CreateScalarSelector<TSource, TInner, TResult>(Func<IEnumerable<TSource>, IEnumerable<TInner>, IEnumerable<object>> selector)
        {
            ParameterExpression source = Expression.Parameter(typeof(IEnumerable<TSource>), "source");
            var inner = Expression.Parameter(typeof(IEnumerable<TInner>), "inner");
            var eval = Expression.Call(Expression.Constant(selector.Target), selector.Method, source, inner);
            var _get = Expression.Call(GetType(), "GetScalarValue", new Type[] { typeof(TResult) }, eval);

            return Expression.Lambda<Func<IEnumerable<TSource>, IEnumerable<TInner>, TResult>>(_get, source, inner).Compile();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private static TResult GetScalarValue<TResult>(IEnumerable<object> resultList)
        {
            object ret = resultList.First();

            // need to think about the actual behavior of executescalar 
            // - this bit here allows subqueries to return objects
            // wrapped in expando objects - this current implementation makes execute scalar flaky around the edges
            if (ret is IDictionary<string, object>)
                return (TResult)((IDictionary<string, object>)ret).Values.First();

            if (ret.GetType().IsTuple())
                return (TResult)ret.GetPropertyOrFieldValue("Item1");

            return (TResult)ret;
        }

        public virtual Func<IEnumerable<TSource>, IEnumerable<TResult>> CreateSelector<TSource, TResult>()
        {
            var list = Expression.Parameter(typeof(IEnumerable<TSource>), "list");

            return Expression.Lambda<Func<IEnumerable<TSource>, IEnumerable<TResult>>>(CreateSelection(list, typeof(TSource), typeof(TResult)), list).Compile();
        }

        internal Expression CreateSelection(ParameterExpression rows, Type tSource, Type tResult)
        {
            if (!IsAggregateOnlyQuery)
            {
                return QueryTree.Select(rows, tSource, tResult, row => CreateRowResult(rows, row, tResult));
            }

            var sequence = QueryTree.Sequence(tSource);
            var whole = Expression.Call(typeof(Enumerable), nameof(Enumerable.Repeat), new[] { sequence }, rows, Expression.Constant(1));

            return QueryTree.Select(whole, sequence, tResult, group => CreateGroupResult(group, tSource, tResult));
        }

        internal virtual Expression CreateRowResult(ParameterExpression rows, ParameterExpression row, Type tResult)
        {
            var columns = ColumnSources.ToList();
            if (tResult == null)
            {
                return columns[0].CreateRowValue(rows, row);
            }

            var names = columns.Select(c => c.ColumnAlias).ToList();
            if (columns.Count > 1)
            {
                RequireDistinctNames(tResult, names);
            }

            return ExpressionFactory.CreateResult(tResult, names, columns.Select(c => c.CreateRowValue(rows, row)).ToList());
        }

        internal Expression CreateGroupResult(ParameterExpression rows, Type tSource, Type tResult)
        {
            var columns = ColumnSources.ToList();
            var values = columns
                .Select(c => c is AggregateNode aggregate ? aggregate.GetCallExpression(tSource, rows) : FirstRowValue(c, rows, tSource))
                .ToList();
            if (tResult == null)
            {
                return values[0];
            }

            var names = columns.Select(c => c.ColumnAlias).ToList();
            RequireDistinctNames(tResult, names);

            return ExpressionFactory.CreateResult(tResult, names, values, true);
        }

        internal static void RequireDistinctNames(Type tResult, IEnumerable<string> names)
        {
            var byName = tResult.IsDictionary() || tResult == typeof(object) || tResult.HasEmptyConstructor();
            var duplicate = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (byName && duplicate != null)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "The select list has more than one column named '{0}'; give each a different name with AS.", duplicate.Key), SqlErrorKind.Semantic);
            }
        }

        private static Expression FirstRowValue(ColumnSource column, ParameterExpression rows, Type tSource)
        {
            var row = Expression.Parameter(tSource, "row");
            var first = Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { tSource }, rows);

            return ParameterReplacer.Replace(column.CreateRowValue(rows, row), row, first);
        }

        public IEnumerable<string> FieldList
        {
            get
            {
                return (from c in ColumnSources
                        from f in c.GetFields()
                        select f).Select(field => field.LookupId).Distinct(StringComparer.OrdinalIgnoreCase);
            }
        }
        public IEnumerable<string> ResultList
        {
            get
            {
                return (from c in ColumnSources
                        select c.ColumnAlias).Distinct(StringComparer.OrdinalIgnoreCase);
            }
        }
        public IEnumerable<ColumnSource> ColumnSources
        {
            get
            {
                // depending on how the select statement is constructed the parser may or may not eliminate some intermediate reductions
                // the where statement here will eliminate those instances where reductions are not eliminated by the parser        
                return FindDescendants<ColumnSource>().Where(item => item.FindChild<ColumnSource>() == null && !item.AncestorsBelow(this).OfType<ComputedColumn>().Any());
            }
        }
    }
}
