using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Predicates.Comparison;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Boolean
{
    [SyntaxNode(RuleConstants.RULE_EXPRESSION_OR)]
    public class OrNode : BinaryExpressionNode
    {
        public OrNode()
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
            return Expression.OrElse(left, right);
        }
    }
}
