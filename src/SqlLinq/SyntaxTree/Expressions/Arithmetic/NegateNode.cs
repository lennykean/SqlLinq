using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Arithmetic
{
    [SyntaxNode(RuleConstants.RULE_NEGATEEXP_MINUS)]
    public class NegateNode : ExpressionNode
    {
        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var value = CreateChildExpression(sourceData, param, 1);
            if (value.Type != typeof(object))
            {
                return Expression.NegateChecked(value);
            }

            return ArithmeticExpressionNode.Negate(value);
        }

        public override string ToSql()
        {
            return "-" + Children[1].ToSql();
        }
    }
}
