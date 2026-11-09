using System.Linq.Expressions;

namespace SqlLinq
{
    public sealed class SqlArgument
    {
        internal SqlArgument(string text, Expression expression)
        {
            Text = text;
            Expression = expression;
        }

        public string Text { get; }

        public Expression Expression { get; }
    }
}
