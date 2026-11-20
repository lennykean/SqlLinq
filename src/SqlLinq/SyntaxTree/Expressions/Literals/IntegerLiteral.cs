using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlLinq.SyntaxTree.Expressions.Literals
{
    [SyntaxNode(RuleConstants.RULE_VALUE_INTEGERLITERAL)]
    public class IntegerLiteral : LiteralNode
    {
        public IntegerLiteral()
            : base(typeof(Int32))
        {
        }

        internal override MethodInfo GetCoercionMethod(Type from)
        {
            return new Func<string, object>(Parse).Method;
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            return Expression.Constant(Value);
        }

        private static object Parse(string s)
        {
            if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
            {
                return whole;
            }

            return long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var wide) ? wide : (object)double.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}
