using System;
using System.Xml.Linq;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.SyntaxTree
{
    public sealed class ArgumentNode : SyntaxNode
    {
        private readonly int _start;

        private readonly int _length;

        internal ArgumentNode(string text, ExpressionNode expression, SqlException error, int start, int length)
        {
            Text = text;
            Expression = expression;
            Error = error;
            _start = start;
            _length = length;
        }

        public string Text { get; }

        public ExpressionNode Expression { get; }

        public SqlException Error { get; }

        public override int Start => _start;

        public override int Length => _length;

        public override string ToSql()
        {
            return Expression?.ToSql() ?? Text;
        }

        public override XElement ToXml()
        {
            return Expression?.ToXml() ?? new XElement("argument", Text);
        }

        internal ArgumentNode Copy(NonTerminalNode call, Func<NonTerminalNode, NonTerminalNode> replacement)
        {
            var expression = (ExpressionNode)Expression?.Copy(replacement);
            if (expression != null)
            {
                expression.Parent = call;
            }

            return new ArgumentNode(Text, expression, Error, _start, _length) { Parent = call };
        }
    }
}
