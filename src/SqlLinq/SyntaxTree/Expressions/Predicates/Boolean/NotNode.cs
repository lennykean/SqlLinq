using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Predicates.Comparison;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Boolean
{
    [SyntaxNode(RuleConstants.RULE_NOTEXP_NOT)]
    public class NotNode : ExpressionNode
    {
        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var value = CreateChildExpression(sourceData, param, 1);

            return Compiled(() => Expression.Not(PredicateExpressionNode.Condition(value)));
        }
    }
}
