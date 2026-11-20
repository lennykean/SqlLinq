using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions
{
    [SyntaxNode(RuleConstants.RULE_VALUE2)]
    internal sealed class AggregateValueNode : ExpressionNode
    {
        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            return CreateChildExpression(sourceData, param, 0);
        }
    }
}
