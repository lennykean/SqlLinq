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
            new MathFunction("abs", "one value, e.g. abs(RPM)", 1, 1, x => Math.Abs(x[0]), x => Math.Abs(x[0])),
            new MathFunction("sign", "one value, e.g. sign(RPM)", 1, 1, x => Math.Sign(x[0]), x => Math.Sign(x[0])),
            new MathFunction("mod", "two values, e.g. mod(RPM, 1000)", 2, 2, x => x[0] % x[1], x => x[0] % x[1]),
            new MathFunction("round", "one or two values, e.g. round(AFR) or round(AFR, 1)", 1, 2, x => x.Length == 1 || x[1] >= 0 ? x[0] : (long)Round(x[0], (int)x[1]), x => Round(x[0], x.Length == 1 ? 0 : (int)x[1])),
            new MathFunction("floor", "one value, e.g. floor(AFR)", 1, 1, x => x[0], x => Math.Floor(x[0])),
            new MathFunction("ceil", "one value, e.g. ceil(AFR)", 1, 1, x => x[0], x => Math.Ceiling(x[0])),
            new MathFunction("ceiling", "one value, e.g. ceiling(AFR)", 1, 1, x => x[0], x => Math.Ceiling(x[0])),
            new MathFunction("power", "two values, e.g. power(RPM, 2)", 2, 2, null, x => Math.Pow(x[0], x[1])),
            new MathFunction("sqrt", "one value, e.g. sqrt(RPM)", 1, 1, null, x => Math.Sqrt(x[0])),
            new MathFunction("exp", "one value, e.g. exp(RPM)", 1, 1, null, x => Math.Exp(x[0])),
            new MathFunction("ln", "one value, e.g. ln(RPM)", 1, 1, null, x => Math.Log(x[0])),
            new MathFunction("log10", "one value, e.g. log10(RPM)", 1, 1, null, x => Math.Log10(x[0])),
            new MathFunction("log", "two values, e.g. log(2, RPM)", 2, 2, null, x => Math.Log(x[1], x[0])),
            new MathFunction("pi", "no values, e.g. pi()", 0, 0, null, x => Math.PI),
            new MathFunction("degrees", "one value, e.g. degrees(Angle)", 1, 1, null, x => x[0] * 180 / Math.PI),
            new MathFunction("radians", "one value, e.g. radians(Angle)", 1, 1, null, x => x[0] * Math.PI / 180),
            new MathFunction("sin", "one value, e.g. sin(Angle)", 1, 1, null, x => Math.Sin(x[0])),
            new MathFunction("cos", "one value, e.g. cos(Angle)", 1, 1, null, x => Math.Cos(x[0])),
            new MathFunction("tan", "one value, e.g. tan(Angle)", 1, 1, null, x => Math.Tan(x[0])),
            new MathFunction("asin", "one value, e.g. asin(Ratio)", 1, 1, null, x => Math.Asin(x[0])),
            new MathFunction("acos", "one value, e.g. acos(Ratio)", 1, 1, null, x => Math.Acos(x[0])),
            new MathFunction("atan", "one value, e.g. atan(Ratio)", 1, 1, null, x => Math.Atan(x[0])),
            new MathFunction("atan2", "two values, e.g. atan2(Y, X)", 2, 2, null, x => Math.Atan2(x[0], x[1])),
        };

        private readonly string _takes;
        private readonly int _min;
        private readonly int _max;
        private readonly Func<long[], long> _whole;
        private readonly Func<double[], double> _real;

        private MathFunction(string name, string takes, int min, int max, Func<long[], long> whole, Func<double[], double> real)
        {
            Name = name;
            _takes = takes;
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
                throw new SqlException(Name + " takes " + _takes + ".");
            }

            var values = Expression.NewArrayInit(typeof(object), arguments.Select(a => Expression.Convert(a.Expression, typeof(object))));

            return Expression.Call(Expression.Constant(this), new Func<object[], object>(Evaluate).Method, values);
        }

        private object Evaluate(object[] values)
        {
            if (values.Contains(null))
            {
                return null;
            }

            if (_whole != null && values.All(ArithmeticExpressionNode.IsWhole))
            {
                var whole = _whole(values.Select(v => Convert.ToInt64(v, CultureInfo.InvariantCulture)).ToArray());

                return values.All(ArithmeticExpressionNode.IsSmall) ? (object)checked((int)whole) : whole;
            }

            return _real(values.Select(v => ArithmeticExpressionNode.ToNumber(v).Value).ToArray());
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
    }
}
