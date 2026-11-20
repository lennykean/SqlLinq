using System;
using System.Linq;
using System.Linq.Expressions;

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

                return Known(ordered, left, right);
            }

            if (left.Type == typeof(object) && right.Type == typeof(object))
            {
                return Known(compare(Expression.Call(typeof(PredicateExpressionNode), nameof(Order), null, left, right), Expression.Constant(0, typeof(int?))));
            }

            return Known(compare(left, right), left, right);
        }

        internal static Expression Known(Expression test, params Expression[] values)
        {
            if (test is BinaryExpression binary && binary.IsLifted && !binary.IsLiftedToNull)
            {
                return Expression.MakeBinary(binary.NodeType, binary.Left, binary.Right, true, binary.Method);
            }

            var unknown = values.Where(MayBeNull).Select(v => (Expression)Expression.Equal(v, Expression.Constant(null, v.Type))).ToList();

            return unknown.Count == 0 ? test : Expression.Condition(unknown.Aggregate(Expression.OrElse), Expression.Constant(null, typeof(bool?)), Expression.Convert(test, typeof(bool?)));
        }

        internal static Expression Condition(Expression value)
        {
            if (value.Type == typeof(object))
            {
                return Known(Expression.Call(typeof(Convert), nameof(Convert.ToBoolean), null, value), value);
            }

            var type = Nullable.GetUnderlyingType(value.Type) ?? value.Type;
            var code = Type.GetTypeCode(type);

            return !type.IsEnum && code >= TypeCode.SByte && code <= TypeCode.Decimal ? Expression.NotEqual(value, Expression.Convert(Expression.Constant(0), value.Type), true, null) : value;
        }

        internal static bool MayBeNull(Expression value)
        {
            if (value is ConstantExpression constant)
            {
                return constant.Value == null;
            }

            if (value.Type.IsValueType && Nullable.GetUnderlyingType(value.Type) == null)
            {
                return false;
            }

            return !(value is UnaryExpression convert && convert.NodeType == ExpressionType.Convert) || MayBeNull(convert.Operand);
        }

        private static int? Order(object left, object right)
        {
            return left == null || right == null ? (int?)null : QuantifiedComparisonNode.Order(SourcesExtensions.KeyOf(left), SourcesExtensions.KeyOf(right));
        }
    }
}
