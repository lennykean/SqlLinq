using System;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_IS_NULL)]
    public class IsNullNode : ExpressionNode
    {
        public IsNullNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var value = CreateChildExpression(sourceData, param, 0);
            if (value.Type.IsValueType && Nullable.GetUnderlyingType(value.Type) == null)
            {
                return Expression.Constant(false);
            }

            return Expression.Equal(value, Expression.Constant(null, value.Type));
        }
    }
}
