using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SqlLinq.SyntaxTree.Expressions;
using UnitsNet;
using UnitsNet.Metadata;

namespace SqlLinq.Units
{
    public sealed class UnitConversions : ISqlFunction
    {
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        private static readonly MethodInfo ApplyMethod = typeof(UnitConversions).GetMethod(nameof(Apply), BindingFlags.NonPublic | BindingFlags.Static);

        private readonly List<QuantityMetadata> _metadata;

        public UnitConversions(IEnumerable<QuantityMetadata> metadata)
        {
            _metadata = metadata.Where(m => m.Unit != null).ToList();
        }

        public string Name => "convert";

        public Expression Compile(IReadOnlyList<SqlArgument> arguments)
        {
            var texts = arguments.Select(a => a.Text).ToList();
            var convert = Conversion(texts);
            var call = string.Join(", ", texts);

            return Expression.Convert(Expression.Call(ApplyMethod, Expression.Convert(arguments[0].Expression, typeof(object)), Expression.Constant(convert), Expression.Constant(call)), typeof(object));
        }

        public ExpressionNode Expand(ExpressionNode expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return (ExpressionNode)expression.Replace(n => n is NodeWithId call && Name.Equals(call.FunctionName, StringComparison.OrdinalIgnoreCase) ? Rewrite(call) : null);
        }

        private ExpressionNode Rewrite(NodeWithId call)
        {
            var args = call.Arguments.Select(a => a.Text).ToList();
            var convert = Conversion(args);

            var offset = 0d;
            var scale = 0d;
            try
            {
                offset = convert(0);
                scale = convert(1) - offset;
                foreach (var x in new[] { -40.0, 7.0, 1000.0 })
                {
                    var expected = scale * x + offset;
                    if (Math.Abs(convert(x) - expected) > 1e-9 * Math.Max(1, Math.Abs(expected)))
                    {
                        throw new NotSupportedException();
                    }
                }
            }
            catch (Exception e)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} isn't a linear conversion, which convert doesn't support.", string.Join(", ", args)), e, SqlErrorKind.Semantic);
            }

            var value = Expand(call.Arguments[0].Expression ?? throw call.Arguments[0].Error);
            var template = (ExpressionNode)SqlSyntaxTree.ParseExpression(string.Format(CultureInfo.InvariantCulture, "((value) * {0} + {1})", Literal(scale), Literal(offset))).Root;

            return (ExpressionNode)template.Replace(n => n is NodeWithId id && id.Function == null && id.Id.LookupId == "value" ? value : null);
        }

        private Func<double, double> Conversion(List<string> args)
        {
            if (args.Count != 2 && args.Count != 3)
            {
                throw new SqlException("convert takes (column, 'to unit') or (value, 'Kind.from unit', 'to unit').", SqlErrorKind.Semantic);
            }

            var fromText = args.Count == 3 ? SqlSyntax.Unquote(args[1]) : null;
            var toText = SqlSyntax.Unquote(args[args.Count - 1]);
            var column = _metadata.FirstOrDefault(m => m.Property.Name.Equals(SqlSyntax.Unbracket(args[0]), StringComparison.OrdinalIgnoreCase));

            return column != null
                ? ColumnConversion(column, fromText, toText)
                : KindConversion(args[0], fromText, toText);
        }

        private static double? Apply(object value, Func<double, double> convert, string call)
        {
            if (value == null)
            {
                return null;
            }

            var code = Type.GetTypeCode(value.GetType());
            if (code < TypeCode.SByte || code > TypeCode.Decimal)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "convert({0}) can't read '{1}', which isn't a number.", call, value), SqlErrorKind.Evaluation);
            }

            try
            {
                return convert(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }
            catch (ArithmeticException e)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "convert({0}) overflowed converting {1}.", call, value), e, SqlErrorKind.Evaluation);
            }
        }

        private static Func<double, double> ColumnConversion(QuantityMetadata column, string fromText, string toText)
        {
            var unit = column.Unit;

            if (fromText != null && !Equals(Choice(column, fromText), unit.UnitInfo.Value))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} is in {1}, not {2}.", column.Property.Name, unit.Name, fromText), SqlErrorKind.Semantic);
            }

            var to = Choice(column, toText);

            if (to.Equals(unit.UnitInfo.Value))
            {
                return x => x;
            }

            var allowed = column.Conversions.First(c => c.UnitInfo.Value.Equals(to));
            var target = UnitMetadata.FromUnitInfo(allowed.UnitInfo, allowed.QuantityType.QuantityInfo);

            return x => (double)(unit.TryConvertQuantity(x, target, out var converted) ? converted : converted.ToUnit(to)).Value;
        }

        private static Enum Choice(QuantityMetadata column, string text)
        {
            var kind = column.Unit.QuantityType.QuantityInfo.Name + ".";
            var unit = Resolve(column, text) ?? (text.StartsWith(kind, StringComparison.OrdinalIgnoreCase) ? Resolve(column, text[kind.Length..]) : null);

            return unit ?? throw new SqlException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1}) can't convert to '{2}'. Its units are {3}.",
                    column.Property.Name,
                    column.Unit.Name,
                    text,
                    string.Join(", ", UnitChoices.For(column).Select(c => c.Symbol.Length > 0 ? c.Symbol : c.Name))), SqlErrorKind.UnknownName);
        }

        private static Enum Resolve(QuantityMetadata column, string text)
        {
            try
            {
                return UnitChoices.Resolve(column, text);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static Func<double, double> KindConversion(string value, string fromText, string toText)
        {
            if (fromText == null)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "convert({0}, '{1}') needs a column with unit metadata. Otherwise name the from unit and its kind, e.g. convert({0}, 'Pressure.kPa', '{1}').", value, toText), SqlErrorKind.Semantic);
            }

            if (!SplitKind(fromText, out var kind, out var fromUnit))
            {
                var kinds = Quantity.Infos.Where(i => Find(fromText, i) != null).Select(i => string.Format(CultureInfo.CurrentCulture, "'{0}.{1}'", i.Name, fromText)).ToList();

                throw new SqlException(
                    kinds.Count == 0
                        ? string.Format(CultureInfo.CurrentCulture, "'{0}' isn't a unit of any kind.", fromText)
                        : string.Format(CultureInfo.CurrentCulture, "{0} has no unit metadata, so name the kind of '{1}': {2}.", value, fromText, string.Join(" or ", kinds)), kinds.Count == 0 ? SqlErrorKind.UnknownName : SqlErrorKind.Semantic);
            }

            var from = Find(fromUnit, kind) ?? throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' isn't a {1} unit.", fromUnit, kind.Name), SqlErrorKind.UnknownName);
            var toUnit = toText;

            if (SplitKind(toText, out var toKind, out var unqualified))
            {
                if (toKind != kind)
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' is {1}, not {2}.", fromText, kind.Name, toKind.Name), SqlErrorKind.Semantic);
                }
                toUnit = unqualified;
            }

            var to = Find(toUnit, kind) ?? throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' isn't a {1} unit.", toText, kind.Name), SqlErrorKind.UnknownName);

            return x => UnitConverter.Convert(x, from, to);
        }

        private static bool SplitKind(string text, out QuantityInfo kind, out string unit)
        {
            var dot = text.IndexOf('.');

            kind = dot > 0 ? Quantity.Infos.FirstOrDefault(i => i.Name.Equals(text[..dot], StringComparison.OrdinalIgnoreCase)) : null;
            unit = kind != null ? text[(dot + 1)..] : null;

            return kind != null;
        }

        private static Enum Find(string text, QuantityInfo kind)
        {
            foreach (var unit in kind.UnitInfos)
            {
                if (unit.Name.Equals(text, StringComparison.OrdinalIgnoreCase)
                    || UnitAbbreviationsCache.Default.GetUnitAbbreviations(kind.UnitType, Convert.ToInt32(unit.Value, CultureInfo.InvariantCulture), English).Contains(text))
                {
                    return unit.Value;
                }
            }

            return null;
        }

        private static string Literal(double value)
        {
            var digits = Math.Abs(value).ToString("0.0###########################", CultureInfo.InvariantCulture);

            return value < 0 ? "(-" + digits + ")" : digits;
        }
    }
}
