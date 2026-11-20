using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Predicates.Comparison;

#pragma warning disable CA1716
namespace SqlLinq.SyntaxTree.Expressions.Predicates.Boolean
#pragma warning restore CA1716
{
    [SyntaxNode(RuleConstants.RULE_ANDEXP_AND)]
    public class AndNode : BinaryExpressionNode
    {
        public AndNode()
        {
        }

        internal override bool Lifts
        {
            get
            {
                return true;
            }
        }

        internal override Expression Operand(Expression operand)
        {
            return PredicateExpressionNode.Condition(operand);
        }

        protected override Expression CreateChildExpression(Expression left, Expression right)
        {
            return Expression.AndAlso(left, right);
        }
    }
}
