using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree
{
    internal sealed class UntypedValues : ExpressionVisitor
    {
        private bool _found;

        private UntypedValues()
        {
        }

        internal static bool In(Expression expression)
        {
            var visitor = new UntypedValues();
            visitor.Visit(expression);

            return visitor._found;
        }

        public override Expression Visit(Expression node)
        {
            if (_found || node == null)
            {
                return node;
            }

            if (node.Type == typeof(object) || node.NodeType == ExpressionType.Dynamic)
            {
                _found = true;

                return node;
            }

            return base.Visit(node);
        }
    }
}
