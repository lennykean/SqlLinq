using System.Collections.Generic;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree
{
    internal sealed class FreeVariables : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _bound = new HashSet<ParameterExpression>();
        private readonly List<ParameterExpression> _free = new List<ParameterExpression>();

        private FreeVariables()
        {
        }

        internal static IEnumerable<ParameterExpression> Of(Expression expression)
        {
            var visitor = new FreeVariables();
            visitor.Visit(expression);

            return visitor._free;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _bound.UnionWith(node.Parameters);

            return base.VisitLambda(node);
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            _bound.UnionWith(node.Variables);

            return base.VisitBlock(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_bound.Contains(node) && !_free.Contains(node))
            {
                _free.Add(node);
            }

            return node;
        }
    }
}
