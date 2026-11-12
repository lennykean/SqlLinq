using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_ORDERLIST_ID_COMMA)]
    [SyntaxNode(RuleConstants.RULE_ORDERLIST_ID)]
    public class OrderByItem : NonTerminalNode
    {
        private Lazy<Identifier> _id;

        public OrderByItem()
        {
            _id = new Lazy<Identifier>(() => CreateIdentifier());
        }

        internal virtual Identifier CreateIdentifier()
        {
            return SqlSyntax.Name(GetTerminal("Id"));
        }

        public Identifier Id
        {
            get
            {
                return _id.Value;
            }
        }

        public MethodCallExpression CreateExpression(Expression param, Type tSource)
        {
            Debug.Assert(param != null);
            Debug.Assert(tSource != null);

            return CreateExpression(param, tSource, "OrderBy");
        }

        public MethodCallExpression CreateThenByExpression(Expression param, Type tSource)
        {
            Debug.Assert(param != null);
            Debug.Assert(tSource != null);

            return CreateExpression(param, tSource, "ThenBy");
        }

        private MethodCallExpression CreateExpression(Expression param, Type tSource, string functionName)
        {
            LambdaExpression lambda = CreateLambaExpression(tSource);

            return Expression.Call(typeof(Enumerable), IsDescending ? functionName + "Descending" : functionName, new Type[] { tSource, lambda.ReturnType }, param, lambda);
        }

        protected virtual LambdaExpression CreateLambaExpression(Type tSource)
        {
            Debug.Assert(tSource != null);
            Debug.Assert(!string.IsNullOrEmpty(Id.LookupId));

            var column = SelectColumn();
            if (column is ComputedColumn || column?.Function != null)
            {
                var row = Expression.Parameter(tSource, "row");
                var rows = Expression.Parameter(typeof(IEnumerable<>).MakeGenericType(tSource), "rows");
                var empty = Expression.Call(typeof(Enumerable), nameof(Enumerable.Empty), new[] { tSource });

                return Expression.Lambda(ParameterReplacer.Replace(column.CreateRowValue(rows, row), rows, empty), row);
            }

            if (column is AggregateNode)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY {0} refers to an aggregate, which needs GROUP BY or an aggregate-only select list.", Id.LookupId), SqlErrorKind.Semantic);
            }

            var item = Expression.Parameter(tSource, "item");

            return Expression.Lambda(SelectNode.CreateFieldSelector(EnclosingSelect(), column == null ? Id : column.Id, item), item);
        }

        internal virtual ColumnSource SelectColumn()
        {
            var columns = EnclosingSelect().Columns;
            if (columns is ColumnsStar)
            {
                return null;
            }

            var list = columns.ColumnSources.ToList();

            return list.FirstOrDefault(c => Id.Qualifier.Length == 0 && string.Equals(c.ColumnAlias, Id.LookupId, StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(c => !(c is AggregateNode) && !(c is ComputedColumn) && string.Equals(c.Id.LookupId, Id.LookupId, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsDescending
        {
            get
            {
                return FindChild<NonTerminalNode>(RuleConstants.RULE_ORDERTYPE_DESC) != null;
            }
        }
    }
}
