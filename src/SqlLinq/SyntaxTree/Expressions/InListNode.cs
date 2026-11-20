using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Literals;

namespace SqlLinq.SyntaxTree.Expressions
{
    [SyntaxNode(RuleConstants.RULE_INLIST_LPAREN_RPAREN)]
    public class InListNode : ExpressionNode
    {
        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            if (FindChild<ExpressionNode>(1) is NullLiteral)
            {
                return Expression.NewArrayInit(typeof(object));
            }

            var list = CreateChildExpression(sourceData, param, 1);

            return list.Type.IsArray ? list : Expression.NewArrayInit(list.Type, list);
        }
    }
}
