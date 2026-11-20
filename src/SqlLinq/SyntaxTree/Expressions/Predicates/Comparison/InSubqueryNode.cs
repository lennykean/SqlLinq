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
                var untyped = TupleSelectNode.CreateUntypedValues(Subquery, results);
                var boxed = Expression.Call(typeof(SourcesExtensions), nameof(SourcesExtensions.KeyOf), null, Expression.Convert(left, typeof(object)));

                return TupleSelectNode.Guard(QueryTree.Bind(boxed, bound => In(untyped, bound, typeof(object))));
            }

            var keys = QueryTree.Select(results, QueryTree.ElementOf(results.Type), key, row => ExpressionFactory.ToKey(row, key));
            var values = QueryTree.Materialize(Subquery, Expression.New(typeof(HashSet<>).MakeGenericType(key).GetConstructor(new[] { QueryTree.Sequence(key) }), keys));
            var value = ExpressionFactory.ToKey(left, key);

            return QueryTree.Simple(left) ? In(values, value, key) : QueryTree.Bind(value, bound => In(values, bound, key));
        }

        private Expression In(Expression values, Expression value, Type key)
        {
            return QueryTree.Bind(values, set =>
            {
                Expression contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { key }, set, value);
                Expression unknown = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { key }, set, Expression.Constant(null, key));
                if (PredicateExpressionNode.MayBeNull(value))
                {
                    var missing = Expression.Equal(value, Expression.Constant(null, key));
                    contains = Expression.AndAlso(Expression.Not(missing), contains);
                    unknown = Expression.Condition(missing, Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { key }, set), unknown);
                }

                var found = Expression.OrElse(Expression.Convert(contains, typeof(bool?)), Expression.Condition(unknown, Expression.Constant(null, typeof(bool?)), Expression.Constant(false, typeof(bool?))));

                return Negated ? Expression.Not(found) : (Expression)found;
            });
        }
    }
}
