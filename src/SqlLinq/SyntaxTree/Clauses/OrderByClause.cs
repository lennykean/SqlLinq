using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Diagnostics;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_ORDERCLAUSE_ORDER_BY)]
    public class OrderByClause : NonTerminalNode
    {
        public OrderByClause()
        {
        }

        public Func<IEnumerable<T>, IEnumerable<T>> CreateEvaluator<T>()
        {
            ParameterExpression param = Expression.Parameter(typeof(IEnumerable<T>), "arg");

            return Expression.Lambda<Func<IEnumerable<T>, IEnumerable<T>>>(Order(param, typeof(T)), param).Compile();
        }

        internal MethodCallExpression Order(Expression source, Type tSource)
        {
            IEnumerable<OrderByItem> items = OrderByItems; // this does a recursive search - cache it since we are using it twice
            OrderByItem first = items.First();
            Debug.Assert(first != null);

            // since ordering after the first uses the ThenBy* methods first create the OrderBy call
            // and then all of the subsequent ThenBy valls
            MethodCallExpression call = first.CreateExpression(source, tSource);

            foreach (OrderByItem orderby in items.Skip(1)) // skip the first one because that was used above
                call = orderby.CreateThenByExpression(call, tSource);

            return call;
        }

        public IEnumerable<OrderByItem> OrderByItems
        {
            get
            {
                return FindDescendants<OrderByItem>();
            }
        }

        internal bool SortsResults
        {
            get
            {
                var select = EnclosingSelect();
                if (select.GroupByClause == null && !select.Columns.IsAggregateOnlyQuery)
                {
                    return false;
                }

                return OrderByItems.Select(i => i.SelectColumn()).Any(c => c is AggregateNode || c is ComputedColumn || c != null && !string.Equals(c.ColumnAlias, c.Id.LocalId, StringComparison.OrdinalIgnoreCase));
            }
        }

        internal Expression OrderResults(Expression results, Type tResult)
        {
            var columns = EnclosingSelect().Columns.ColumnSources.ToList();
            var names = columns.Select(c => c.ColumnAlias).ToList();
            var single = names.Count == 1 && !tResult.IsDictionary();
            var keys = OrderByItems.Select(i => (Key(i, columns, names, single), i.IsDescending)).ToList();

            return ResultOrder.Order(results, tResult, keys);
        }

        private static Func<Expression, Expression> Key(OrderByItem item, List<ColumnSource> columns, List<string> names, bool single)
        {
            if (item is OrderByValueItem)
            {
                return row => row;
            }

            var index = columns.IndexOf(item.SelectColumn());
            if (index < 0)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY {0} isn't a column of the select list.", item.Id.LookupId));
            }

            return row => ResultOrder.Column(row, names[index], index, single);
        }
    }
}
