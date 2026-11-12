using System;
using System.Globalization;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Arithmetic
{
    public abstract class ArithmeticExpressionNode : BinaryExpressionNode
    {
        protected ArithmeticExpressionNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var left = CreateChildExpression(sourceData, param, 0);
            var right = CreateChildExpression(sourceData, param, 2);

            if (left.Type != typeof(object) && right.Type != typeof(object))
            {
                return CreateBinaryExpression(Lifted(left, right.Type), Lifted(right, left.Type));
            }

            var boxedLeft = Expression.Variable(typeof(object), "left");
            var boxedRight = Expression.Variable(typeof(object), "right");

            return Expression.Block(
                new[] { boxedLeft, boxedRight },
                Expression.Assign(boxedLeft, Expression.Convert(left, typeof(object))),
                Expression.Assign(boxedRight, Expression.Convert(right, typeof(object))),
                WholeOrReal(boxedLeft, boxedRight, CreateChildExpression));
        }

        private static Expression Lifted(Expression value, Type other)
        {
            var nullable = Nullable.GetUnderlyingType(value.Type) != null || Nullable.GetUnderlyingType(other) != null;
            if (!nullable || !value.Type.IsValueType || !other.IsValueType)
            {
                return value;
            }

            var widest = (Nullable.GetUnderlyingType(value.Type) ?? value.Type).Widest(Nullable.GetUnderlyingType(other) ?? other);
            var lifted = widest == null || !widest.IsValueType ? value.Type : typeof(Nullable<>).MakeGenericType(widest);

            return lifted == value.Type ? value : Expression.Convert(value, lifted);
        }

        internal static BlockExpression Negate(Expression value)
        {
            var boxed = Expression.Variable(typeof(object), "value");

            return Expression.Block(
                new[] { boxed },
                Expression.Assign(boxed, Expression.Convert(value, typeof(object))),
                WholeOrReal(boxed, boxed, (operand, same) => Expression.NegateChecked(operand)));
        }

        private static ConditionalExpression WholeOrReal(ParameterExpression left, ParameterExpression right, Func<Expression, Expression, Expression> operate)
        {
            var whole = Expression.Call(new Func<object, object, long, object>(Narrow).Method, left, right, operate(ToWhole(left), ToWhole(right)));
            var real = Expression.Convert(operate(ToNumber(left), ToNumber(right)), typeof(object));

            return Expression.Condition(Expression.Call(new Func<object, object, bool>(AreWhole).Method, left, right), whole, real);
        }

        internal static Expression ToNumber(Expression value)
        {
            return Expression.Call(new Func<object, double?>(ToNumber).Method, Expression.Convert(value, typeof(object)));
        }

        private static MethodCallExpression ToWhole(Expression value)
        {
            return Expression.Call(new Func<object, long>(ToWhole).Method, value);
        }

        private static long ToWhole(object value)
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static bool AreWhole(object left, object right)
        {
            return IsWhole(left) && IsWhole(right);
        }

        internal static bool IsWhole(object value)
        {
            var code = value == null ? TypeCode.Empty : Type.GetTypeCode(value.GetType());

            return !(value is Enum) && code >= TypeCode.SByte && code <= TypeCode.UInt64;
        }

        internal static bool IsSmall(object value)
        {
            return Type.GetTypeCode(value.GetType()) <= TypeCode.Int32;
        }

        private static object Narrow(object left, object right, long value)
        {
            var small = IsSmall(left) && IsSmall(right);

            return Convert.ChangeType(value, small ? typeof(int) : typeof(long), CultureInfo.InvariantCulture);
        }

        internal static double? ToNumber(object value)
        {
            if (value == null)
            {
                return null;
            }

            var code = Type.GetTypeCode(value.GetType());
            if (value is Enum || code < TypeCode.SByte || code > TypeCode.Decimal)
            {
                throw new SqlException("Arithmetic requires numeric operands; received " + value.GetType().Name + ".", SqlErrorKind.Evaluation);
            }

            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }
}
