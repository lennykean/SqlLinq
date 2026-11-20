using System;
using System.Reflection;
using System.Globalization;

namespace SqlLinq.SyntaxTree.Expressions.Literals
{
    [SyntaxNode(RuleConstants.RULE_VALUE_DATELITERAL)]
    public class DateLiteral : LiteralNode
    {
        public DateLiteral()
            : base(typeof(DateTime))
        {
        }

        internal override MethodInfo GetCoercionMethod(Type from)
        {
            if (from == typeof(string))
                return new Func<string, DateTime>(DateTime.Parse).Method;

            return new Func<object, DateTime>(o => DateTime.Parse(o.ToString(), CultureInfo.CurrentCulture)).Method;
        }

        internal override object Value
        {
            get
            {
                var text = GetTerminalText(GetType().Name);
                if (!DateTime.TryParse(text.Trim('#'), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be read because it isn't a date.", text), SqlErrorKind.Lexical, Start, Length);
                }

                return date;
            }
        }
    }
}
