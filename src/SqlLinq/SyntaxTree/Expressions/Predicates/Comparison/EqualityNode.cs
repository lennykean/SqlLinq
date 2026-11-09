using System;
using System.Reflection;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_EQ)]
    public class EqualityNode : PredicateExpressionNode
    {
        public EqualityNode()
        {
        }

        protected override Expression CreateChildExpression(Expression left, Expression right)
        {
            if (left.Type == typeof(object) && right.Type == typeof(object))
            {
                return Expression.Call(typeof(EqualityNode), nameof(ValuesEqual), null, left, right);
            }

            return Expression.Equal(left, right);
        }

        internal static bool ValuesEqual(object left, object right)
        {
            return Equals(SourcesExtensions.KeyOf(left), SourcesExtensions.KeyOf(right));
        }
    }
}
