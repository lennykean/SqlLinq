using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_EXCLAMEQ)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_LTGT)]
    public class InequalityNode : PredicateExpressionNode
    {
        public InequalityNode()
        {
        }

        protected override Expression CreateChildExpression(Expression left, Expression right)
        {
            if (left.Type == typeof(object) && right.Type == typeof(object))
            {
                return Expression.Not(Expression.Call(typeof(EqualityNode), nameof(EqualityNode.ValuesEqual), null, left, right));
            }

            return Known(Expression.NotEqual(left, right), left, right);
        }
    }
}
