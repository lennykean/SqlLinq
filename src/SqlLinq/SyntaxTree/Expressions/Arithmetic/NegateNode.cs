using System;
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
                var underlying = Nullable.GetUnderlyingType(value.Type) ?? value.Type;
                var type = underlying.IsPrimitive && underlying != typeof(bool) ? underlying.Widest(typeof(int)).Lifted(value.Type) : value.Type;

                return Compiled(() => Expression.NegateChecked(type == value.Type ? value : Expression.Convert(value, type)));
            }

            return ArithmeticExpressionNode.Negate(value);
        }

        public override string ToSql()
        {
            return "-" + Children[1].ToSql();
        }
    }
}
