using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Expressions
{
    [SyntaxNode(RuleConstants.RULE_TUPLE_LPAREN_RPAREN)]
    public class TupleSelectNode : ExpressionNode
    {
        public TupleSelectNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var select = FindChild<SelectNode>(1);
            if (QueryTree.Natural(select))
            {
                return QueryTree.Materialize(select, Single(Subquery(select, sourceData, param, null)));
            }

            var results = Subquery(select, sourceData, param, typeof(IDictionary<string, object>));
            var value = QueryTree.Materialize(select, Expression.Call(typeof(TupleSelectNode), nameof(ScalarValue), null, results));

            return Expression.Convert(value, ResultType(select, RowType(select, sourceData)));
        }

        internal static Expression CreateResults(SelectNode select, ParameterExpression sourceData, ParameterExpression param)
        {
            if (QueryTree.Natural(select))
            {
                return Subquery(select, sourceData, param, null);
            }

            var rows = Subquery(select, sourceData, param, typeof(IDictionary<string, object>));

            return QueryTree.Select(
                rows,
                typeof(IDictionary<string, object>),
                typeof(object),
                row => Expression.Call(typeof(Enumerable), nameof(Enumerable.First), new[] { typeof(object) }, Expression.Property(row, nameof(IDictionary<string, object>.Values))));
        }

        internal static Expression CreateUntypedValues(SelectNode select, Expression results)
        {
            var values = QueryTree.ElementOf(results.Type) == typeof(object)
                ? results
                : Expression.Call(typeof(Enumerable), nameof(Enumerable.Cast), new[] { typeof(object) }, results);

            return QueryTree.Materialize(select, Expression.Call(typeof(TupleSelectNode), nameof(Values), null, values));
        }

        internal static Expression CreateExists(SelectNode select, ParameterExpression sourceData, ParameterExpression param)
        {
            var natural = select.Columns is ColumnsStar || QueryTree.Natural(select);
            var results = Subquery(select, sourceData, param, natural ? null : typeof(object));

            return QueryTree.Materialize(select, Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { QueryTree.ElementOf(results.Type) }, results));
        }

        internal static void RequireOneColumn(SelectNode select, string keyword)
        {
            if (select.Columns is ColumnsStar || select.Columns.ColumnSources.Count() != 1)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "The subquery of {0} must select exactly one column.", keyword), SqlErrorKind.Semantic);
            }
        }

        internal static Expression Guard(Expression predicate)
        {
            if (!UntypedValues.In(predicate))
            {
                foreach (var row in DereferencedRows.In(predicate))
                {
                    predicate = Expression.AndAlso(Expression.NotEqual(row, Expression.Constant(null, row.Type)), predicate);
                }

                return predicate;
            }

            return Expression.TryCatch(
                predicate,
                Expression.Catch(typeof(SqlException), Expression.Rethrow(typeof(bool))),
                Expression.Catch(typeof(OperationCanceledException), Expression.Rethrow(typeof(bool))),
                Expression.Catch(typeof(Exception), Expression.Constant(false)));
        }

        private static Expression Subquery(SelectNode select, ParameterExpression sourceData, ParameterExpression param, Type resultType)
        {
            var rowType = RowType(select, sourceData);
            var rows = select.Source == null ? sourceData : SourcesExtensions.Sequence(select.Source, rowType);
            select.OuterRow = param;

            return QueryTree.Build(select, rows, rowType, resultType);
        }

        private static Expression Single(Expression results)
        {
            var element = QueryTree.ElementOf(results.Type);
            var value = element.IsValueType && Nullable.GetUnderlyingType(element) == null ? typeof(Nullable<>).MakeGenericType(element) : element;
            var list = typeof(List<>).MakeGenericType(element);
            var found = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.ToList),
                new[] { element },
                Expression.Call(typeof(Enumerable), nameof(Enumerable.Take), new[] { element }, results, Expression.Constant(2)));

            return QueryTree.Bind(found, rows =>
            {
                var first = value == element ? rows : QueryTree.Select(rows, element, value, row => Expression.Convert(row, value));
                var error = Expression.New(typeof(SqlException).GetConstructor(new[] { typeof(string) }), Expression.Constant("The subquery returned more than one row."));

                return Expression.Condition(
                    Expression.GreaterThan(Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), new[] { element }, rows), Expression.Constant(1)),
                    Expression.Throw(error, value),
                    Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { value }, first));
            });
        }

        private static Type RowType(SelectNode select, ParameterExpression sourceData)
        {
            return select.Source == null ? sourceData.Type.GetGenericArguments()[0] : SourcesExtensions.ElementType(select.Source);
        }

        private static object ScalarValue(IEnumerable<IDictionary<string, object>> results)
        {
            var rows = results.Take(2).ToList();
            if (rows.Count > 1)
            {
                throw new SqlException("The subquery returned more than one row.", SqlErrorKind.Evaluation);
            }

            return rows.Count == 0 ? null : rows[0].Values.First();
        }

        private static HashSet<object> Values(IEnumerable<object> results)
        {
            return new HashSet<object>(results.Select(SourcesExtensions.KeyOf));
        }

        private static Type ResultType(SelectNode select, Type rowType)
        {
            if (select.Columns.ColumnSources.FirstOrDefault() is AggregateNode aggregate)
            {
                var type = aggregate.GetCallExpression(rowType, Expression.Parameter(typeof(IEnumerable<>).MakeGenericType(rowType))).Type;
                if (type.IsValueType)
                {
                    return type;
                }
            }

            return typeof(object);
        }
    }
}
