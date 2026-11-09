using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_EXISTS_LPAREN_RPAREN)]
    internal sealed class ExistsNode : ExpressionNode
    {
        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            return TupleSelectNode.CreateExists(Children.OfType<SelectNode>().Single(), sourceData, param);
        }
    }
}
