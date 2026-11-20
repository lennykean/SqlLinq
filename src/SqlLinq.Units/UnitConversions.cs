using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;
using UnitsNet;
using UnitsNet.Metadata;

namespace SqlLinq.Units
{
    public sealed class UnitConversions : ISqlFunction
    {
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        private static readonly MethodInfo ApplyMethod = new Func<object, Func<double, double>, string, double?>(Apply).Method;

        private readonly List<QuantityMetadata> _metadata;

        public UnitConversions(IEnumerable<QuantityMetadata> metadata)
        {
            _metadata = metadata.Where(m => m.Unit != null).ToList();
        }

        public string Name => "convert";

        public Expression Compile(IReadOnlyList<SqlArgument> arguments)
        {
            var texts = arguments.Select(a => a.Text).ToList();
            var argument = arguments.Count > 0 ? arguments[0].Expression : null;
            var value = argument is UnaryExpression boxed && boxed.NodeType == ExpressionType.Convert ? boxed.Operand : argument;
            var convert = Conversion(texts, (value as MemberExpression)?.Member.DeclaringType);
            var call = string.Join(", ", texts);
            var type = Nullable.GetUnderlyingType(value.Type) ?? value.Type;
            var code = Type.GetTypeCode(type);
            if (type != typeof(object) && !type.IsAbstract && (code < TypeCode.SByte || code > TypeCode.Decimal))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "convert({0}) can't read {1} values because they aren't numbers.", call, type.Name), SqlErrorKind.Semantic);
            }

            if (type != typeof(object) && !type.IsAbstract && !type.IsEnum)
            {
                return Expression.Call(new Func<double?, Func<double, double>, string, double?>(Apply).Method, Expression.Convert(value, typeof(double?)), Expression.Constant(convert), Expression.Constant(call));
            }

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
            Func<double, double> convert;
            try
            {
                convert = Conversion(args, null);
            }
            catch (SqlException e) when (e.Start < 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, call.Start, call.Length);
            }

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

        private Func<double, double> Conversion(List<string> args, Type owner)
        {
            if (args.Count != 2 && args.Count != 3)
            {
                throw new SqlException("convert can't run because it takes 2 or 3 arguments, not " + args.Count + ".", SqlErrorKind.Semantic);
            }

            var fromText = args.Count == 3 ? SqlSyntax.Unquote(args[1]) : null;
            var toText = SqlSyntax.Unquote(args[args.Count - 1]);
            var name = SqlSyntax.Tokenize(args[0]).Count == 1 ? SqlSyntax.Unbracket(new Identifier(SqlSyntax.Unbracket(args[0])).LocalId) : args[0];
            var column = _metadata.FirstOrDefault(m => m.Property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && (owner == null || m.Property.DeclaringType.IsAssignableFrom(owner) || owner.IsAssignableFrom(m.Property.DeclaringType)));

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

            return Apply(Convert.ToDouble(value, CultureInfo.InvariantCulture), convert, call);
        }

        private static double? Apply(double? value, Func<double, double> convert, string call)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                var number = value.Value;
                return double.IsFinite(number) ? convert(number) : number;
            }
            catch (Exception e) when (e is ArithmeticException || e is ArgumentException)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "convert({0}) can't convert {1} because the result is out of range.", call, value), e, SqlErrorKind.Evaluation);
            }
        }

        private static Func<double, double> ColumnConversion(QuantityMetadata column, string fromText, string toText)
        {
            var unit = column.Unit;

            if (fromText != null && !Equals(Unit(column, fromText), unit.UnitInfo.Value))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be converted from {2} because it's in {1}.", column.Property.Name, unit.Name, fromText), SqlErrorKind.Semantic);
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
            var choices = UnitChoices.For(column);
            var kind = column.Unit.QuantityType.QuantityInfo.Name + ".";
            var symbol = text.StartsWith(kind, StringComparison.OrdinalIgnoreCase) ? text[kind.Length..] : text;
            var shared = choices.Where(c => c.Symbol == symbol).Select(c => "'" + c.Name + "'").ToList();

            return Unit(column, text) ?? throw new SqlException(
                shared.Count > 1
                    ? string.Format(CultureInfo.CurrentCulture, "'{0}' can't be resolved for {1} ({2}) because it could be {3}.", text, column.Property.Name, column.Unit.Name, string.Join(" or ", shared))
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} ({1}) can't convert to '{2}'. Its units are {3}.",
                        column.Property.Name,
                        column.Unit.Name,
                        text,
                        string.Join(", ", choices.Select(c => c.Symbol.Length > 0 && choices.Count(o => o.Symbol == c.Symbol) == 1 ? c.Symbol : c.Name))), SqlErrorKind.UnknownName);
        }

        private static Enum Unit(QuantityMetadata column, string text)
        {
            var kind = column.Unit.QuantityType.QuantityInfo.Name + ".";

            return Resolve(column, text) ?? (text.StartsWith(kind, StringComparison.OrdinalIgnoreCase) ? Resolve(column, text[kind.Length..]) : null);
        }

        private static Enum Resolve(QuantityMetadata column, string text)
        {
            try
            {
                return UnitChoices.Resolve(column, text);
            }
            catch (ArgumentException)
            {
                var unit = Find(text, column.Unit.QuantityType.QuantityInfo);

                return column.Conversions.Select(c => c.UnitInfo.Value).Append(column.Unit.UnitInfo.Value).Contains(unit) ? unit : null;
            }
        }

        private static Func<double, double> KindConversion(string value, string fromText, string toText)
        {
            if (fromText == null)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be converted without unit metadata or a from unit.", value), SqlErrorKind.Semantic);
            }

            if (!SplitKind(fromText, out var kind, out var fromUnit))
            {
                var kinds = Quantity.Infos.Where(i => Find(fromText, i) != null).Select(i => string.Format(CultureInfo.CurrentCulture, "'{0}.{1}'", i.Name, fromText)).ToList();

                throw new SqlException(
                    kinds.Count == 0
                        ? string.Format(CultureInfo.CurrentCulture, "'{0}' can't be converted because it names no unit or more than one.", fromText)
                        : string.Format(CultureInfo.CurrentCulture, "{0} has no unit metadata, so '{1}' can't be converted without its kind: {2}.", value, fromText, string.Join(" or ", kinds)), kinds.Count == 0 ? SqlErrorKind.UnknownName : SqlErrorKind.Semantic);
            }

            var from = Find(fromUnit, kind) ?? throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' can't be used because it names no {1} unit or more than one.", fromUnit, kind.Name), SqlErrorKind.UnknownName);
            var toUnit = toText;

            if (SplitKind(toText, out var toKind, out var unqualified))
            {
                if (toKind != kind)
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' can't be converted to {2} because it's {1}.", fromText, kind.Name, toKind.Name), SqlErrorKind.Semantic);
                }
                toUnit = unqualified;
            }

            var to = Find(toUnit, kind) ?? throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' can't be used because it names no {1} unit or more than one.", toText, kind.Name), SqlErrorKind.UnknownName);

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
            return kind.UnitInfos.FirstOrDefault(u => u.Name.Equals(text, StringComparison.OrdinalIgnoreCase))?.Value ?? (UnitParser.Default.TryParse(text, kind.UnitType, English, out Enum unit) ? unit : null);
        }

        private static string Literal(double value)
        {
            var digits = Math.Abs(value).ToString("0.0###########################", CultureInfo.InvariantCulture);

            return value < 0 ? "(-" + digits + ")" : digits;
        }
    }
}
