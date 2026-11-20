using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_GT)]
    public class GreaterThanNode : PredicateExpressionNode
    {
        public GreaterThanNode()
        {
        }

        protected override Expression CreateChildExpression(Expression left, Expression right)
        {
            return Compare(left, right, Expression.GreaterThan);
        }
    }
}
