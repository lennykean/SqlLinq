using System;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions
{
    public abstract class BinaryExpressionNode : ExpressionNode
    {
        protected BinaryExpressionNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var left = CreateChildExpression(sourceData, param, 0);
            var right = CreateChildExpression(sourceData, param, 2);

            return Compiled(() => CreateBinaryExpression(Operand(left), Operand(right)));
        }

        protected Expression CreateBinaryExpression(Expression left, Expression right)
        {
            Type widest = right.Type.Widest(left.Type);
            if (Lifts)
            {
                widest = widest?.Lifted(left.Type, right.Type);
            }

            if (right.Type != widest)
            {
                right = ExpressionFactory.ConvertTo(right, widest);
            }

            if (left.Type != widest)
            {
                left = ExpressionFactory.ConvertTo(left, widest);
            }

            var body = CreateChildExpression(left, right);

            return WrapReturnExpression(body);
        }

        internal virtual bool Lifts
        {
            get
            {
                return false;
            }
        }

        internal virtual Expression Operand(Expression operand)
        {
            return operand;
        }

        protected virtual Expression WrapReturnExpression(Expression body)
        {
            return body;
        }

        protected abstract Expression CreateChildExpression(Expression left, Expression right);
    }
}
