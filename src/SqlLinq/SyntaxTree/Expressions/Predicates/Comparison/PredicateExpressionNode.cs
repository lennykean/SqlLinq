using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Diagnostics;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    public abstract class PredicateExpressionNode : BinaryExpressionNode
    {
        internal override bool Lifts
        {
            get
            {
                return true;
            }
        }

        protected override Expression WrapReturnExpression(Expression body)
        {
            return TupleSelectNode.Guard(body);
        }

        internal static Expression Compare(Expression left, Expression right, Func<Expression, Expression, Expression> compare)
        {
            if (left.Type == typeof(string) && right.Type == typeof(string))
            {
                var ordered = compare(Expression.Call(typeof(string), nameof(string.CompareOrdinal), null, left, right), Expression.Constant(0));

                return Known(left, Known(right, ordered));
            }

            if (left.Type == typeof(object) && right.Type == typeof(object))
            {
                return compare(Expression.Call(typeof(PredicateExpressionNode), nameof(Order), null, left, right), Expression.Constant(0));
            }

            return compare(left, right);
        }

        private static Expression Known(Expression value, Expression test)
        {
            return value is ConstantExpression constant && constant.Value != null
                ? test
                : Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, value.Type)), test);
        }

        private static int Order(object left, object right)
        {
            return QuantifiedComparisonNode.Order(SourcesExtensions.KeyOf(left), SourcesExtensions.KeyOf(right));
        }
    }
}
