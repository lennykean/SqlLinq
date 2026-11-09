using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_IN_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_NOT_IN_LPAREN_RPAREN)]
    internal sealed class InSubqueryNode : ExpressionNode
    {
        private bool Negated
        {
            get
            {
                return Rule.Index == (int)RuleConstants.RULE_PREDEXP_NOT_IN_LPAREN_RPAREN;
            }
        }

        private SelectNode Subquery
        {
            get
            {
                return Children.OfType<SelectNode>().Single();
            }
        }

        internal override void CheckSyntax()
        {
            TupleSelectNode.RequireOneColumn(Subquery, "IN");
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var left = CreateChildExpression(sourceData, param, 0);
            var results = TupleSelectNode.CreateResults(Subquery, sourceData, param);
            var key = left.Type.KeyType(QueryTree.ElementOf(results.Type));
            if (key == null)
            {
                var body = Expression.Call(
                    typeof(InSubqueryNode),
                    nameof(Matches),
                    null,
                    Expression.Convert(left, typeof(object)),
                    TupleSelectNode.CreateUntypedValues(Subquery, results),
                    Expression.Constant(Negated));

                return TupleSelectNode.Guard(body);
            }

            var keys = QueryTree.Select(results, QueryTree.ElementOf(results.Type), key, row => ExpressionFactory.ToKey(row, key));
            var values = QueryTree.Materialize(Subquery, Expression.New(typeof(HashSet<>).MakeGenericType(key).GetConstructor(new[] { QueryTree.Sequence(key) }), keys));
            var value = ExpressionFactory.ToKey(left, key);

            return QueryTree.Simple(left) ? Typed(values, value, key) : QueryTree.Bind(value, bound => Typed(values, bound, key));
        }

        private Expression Typed(Expression values, Expression value, Type key)
        {
            return QueryTree.Bind(values, set =>
            {
                var known = Expression.NotEqual(value, Expression.Constant(null, key));
                var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { key }, set, value);
                if (!Negated)
                {
                    return Expression.AndAlso(known, contains);
                }

                var empty = Expression.Not(Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { key }, set));
                var unknown = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { key }, set, Expression.Constant(null, key));

                return Expression.OrElse(empty, Expression.AndAlso(known, Expression.AndAlso(Expression.Not(unknown), Expression.Not(contains))));
            });
        }

        private static bool Matches(object value, HashSet<object> values, bool negated)
        {
            if (!negated)
            {
                return value != null && values.Contains(SourcesExtensions.KeyOf(value));
            }

            return values.Count == 0 || value != null && !values.Contains(null) && !values.Contains(SourcesExtensions.KeyOf(value));
        }
    }
}
