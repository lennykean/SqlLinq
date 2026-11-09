using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree
{
    internal sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly Expression _replacement;
        private int _uses;
        private int _lambdas;

        private ParameterReplacer(ParameterExpression parameter, Expression replacement)
        {
            _parameter = parameter;
            _replacement = replacement;
        }

        internal static Expression Replace(Expression body, ParameterExpression parameter, Expression replacement)
        {
            return new ParameterReplacer(parameter, replacement).Visit(body);
        }

        internal static int Uses(Expression body, ParameterExpression parameter)
        {
            var counter = new ParameterReplacer(parameter, parameter);
            counter.Visit(body);

            return counter._uses;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _lambdas++;
            try
            {
                return base.VisitLambda(node);
            }
            finally
            {
                _lambdas--;
            }
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node != _parameter)
            {
                return node;
            }

            _uses += _lambdas == 0 ? 1 : 2;

            return _replacement;
        }
    }
}
