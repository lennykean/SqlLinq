using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Arithmetic;

namespace SqlLinq
{
    sealed class MathFunction : ISqlFunction
    {
        internal static readonly MathFunction[] All =
        {
            new MathFunction("abs", 1, 1, x => Math.Abs(x[0]), x => Math.Abs(x[0])),
            new MathFunction("sign", 1, 1, x => Math.Sign(x[0]), x => double.IsNaN(x[0]) ? x[0] : Math.Sign(x[0])),
            new MathFunction("mod", 2, 2, x => x[0] % x[1], x => x[0] % x[1]),
            new MathFunction("round", 1, 2, x => x.Length == 1 || x[1] >= 0 ? x[0] : (long)Round(x[0], (int)x[1]), x => Round(x[0], x.Length == 1 ? 0 : (int)x[1])),
            new MathFunction("floor", 1, 1, x => x[0], x => Math.Floor(x[0])),
            new MathFunction("ceil", 1, 1, x => x[0], x => Math.Ceiling(x[0])),
            new MathFunction("ceiling", 1, 1, x => x[0], x => Math.Ceiling(x[0])),
            new MathFunction("power", 2, 2, null, x => Math.Pow(x[0], x[1])),
            new MathFunction("sqrt", 1, 1, null, x => Math.Sqrt(x[0])),
            new MathFunction("exp", 1, 1, null, x => Math.Exp(x[0])),
            new MathFunction("ln", 1, 1, null, x => Math.Log(x[0])),
            new MathFunction("log10", 1, 1, null, x => Math.Log10(x[0])),
            new MathFunction("log", 2, 2, null, x => Math.Log(x[1], x[0])),
            new MathFunction("pi", 0, 0, null, x => Math.PI),
            new MathFunction("degrees", 1, 1, null, x => x[0] * 180 / Math.PI),
            new MathFunction("radians", 1, 1, null, x => x[0] * Math.PI / 180),
            new MathFunction("sin", 1, 1, null, x => Math.Sin(x[0])),
            new MathFunction("cos", 1, 1, null, x => Math.Cos(x[0])),
            new MathFunction("tan", 1, 1, null, x => Math.Tan(x[0])),
            new MathFunction("asin", 1, 1, null, x => Math.Asin(x[0])),
            new MathFunction("acos", 1, 1, null, x => Math.Acos(x[0])),
            new MathFunction("atan", 1, 1, null, x => Math.Atan(x[0])),
            new MathFunction("atan2", 2, 2, null, x => Math.Atan2(x[0], x[1])),
        };

        private readonly int _min;
        private readonly int _max;
        private readonly Expression<Func<long[], long>> _whole;
        private readonly Expression<Func<double[], double>> _real;
        private Func<long[], long> _wholeFunction;
        private Func<double[], double> _realFunction;

        private MathFunction(string name, int min, int max, Expression<Func<long[], long>> whole, Expression<Func<double[], double>> real)
        {
            Name = name;
            _min = min;
            _max = max;
            _whole = whole;
            _real = real;
        }

        public string Name { get; }

        public Expression Compile(IReadOnlyList<SqlArgument> arguments)
        {
            if (arguments.Count < _min || arguments.Count > _max)
            {
                throw new SqlException(Name + " can't be calculated because it takes " + (_max == 0 ? "no arguments" : _min == _max ? _max + (_max == 1 ? " argument" : " arguments") : _min + " or " + _max + " arguments") + ", not " + arguments.Count + ".", SqlErrorKind.Semantic);
            }

            var types = arguments.Select(a => Nullable.GetUnderlyingType(a.Expression.Type) ?? a.Expression.Type).ToList();
            if (types.Contains(typeof(object)))
            {
                var values = Expression.NewArrayInit(typeof(object), arguments.Select(a => Expression.Convert(a.Expression, typeof(object))));

                return Expression.Call(Expression.Constant(this), new Func<object[], object>(Evaluate).Method, values);
            }

            var odd = types.FirstOrDefault(t => t.IsEnum || Type.GetTypeCode(t) < TypeCode.SByte || Type.GetTypeCode(t) > TypeCode.Decimal);
            if (odd != null)
            {
                throw new SqlException("Arithmetic can't use " + odd.Name + " values because they aren't numbers.", SqlErrorKind.Semantic);
            }

            var whole = _whole != null && types.All(t => Type.GetTypeCode(t) <= TypeCode.UInt64);
            var number = whole ? typeof(long) : typeof(double);
            var numbers = arguments.Select(a => Nullable.GetUnderlyingType(a.Expression.Type) == null ? a.Expression : Expression.Property(a.Expression, "Value")).Select(v => v.Type == number ? v : Expression.ConvertChecked(v, number)).ToList();
            var result = new Arguments(whole ? _whole.Parameters[0] : _real.Parameters[0], numbers).Visit(whole ? _whole.Body : _real.Body);
            if (whole && types.All(t => Type.GetTypeCode(t) <= TypeCode.Int32))
            {
                result = Expression.ConvertChecked(result, typeof(int));
            }

            var missing = arguments.Select(a => a.Expression).Where(v => Nullable.GetUnderlyingType(v.Type) != null).Select(v => (Expression)Expression.Equal(v, Expression.Constant(null, v.Type))).ToList();
            if (missing.Count == 0)
            {
                return result;
            }

            var lifted = typeof(Nullable<>).MakeGenericType(result.Type);

            return Expression.Condition(missing.Aggregate(Expression.OrElse), Expression.Constant(null, lifted), Expression.Convert(result, lifted));
        }

        private object Evaluate(object[] values)
        {
            if (values.Contains(null))
            {
                return null;
            }

            if (_whole != null && values.All(ArithmeticExpressionNode.IsWhole))
            {
                var whole = (_wholeFunction ??= _whole.Compile())(values.Select(v => Convert.ToInt64(v, CultureInfo.InvariantCulture)).ToArray());

                return values.All(ArithmeticExpressionNode.IsSmall) ? (object)checked((int)whole) : whole;
            }

            return (_realFunction ??= _real.Compile())(values.Select(v => ArithmeticExpressionNode.ToNumber(v).Value).ToArray());
        }

        private static double Round(double value, int digits)
        {
            if (digits >= 0)
            {
                return Math.Round(value, Math.Min(digits, 15), MidpointRounding.AwayFromZero);
            }

            var scale = Math.Pow(10, -digits);

            return Math.Round(value / scale, MidpointRounding.AwayFromZero) * scale;
        }

        private sealed class Arguments : ExpressionVisitor
        {
            private readonly ParameterExpression _values;
            private readonly IReadOnlyList<Expression> _arguments;

            internal Arguments(ParameterExpression values, IReadOnlyList<Expression> arguments)
            {
                _values = values;
                _arguments = arguments;
            }

            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.NodeType != ExpressionType.ArrayIndex || node.Left != _values)
                {
                    return base.VisitBinary(node);
                }

                var index = (int)((ConstantExpression)node.Right).Value;

                return index < _arguments.Count ? _arguments[index] : Expression.Default(node.Type);
            }

            protected override Expression VisitUnary(UnaryExpression node)
            {
                return node.NodeType == ExpressionType.ArrayLength && node.Operand == _values ? Expression.Constant(_arguments.Count) : base.VisitUnary(node);
            }
        }
    }
}
