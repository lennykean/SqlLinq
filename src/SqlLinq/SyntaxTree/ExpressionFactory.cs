using System;
using System.Linq;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;

namespace SqlLinq
{
    static class ExpressionFactory
    {
        public static Expression CreateResult(Type tResult, IList<string> names, IList<Expression> values)
        {
            return CreateResult(tResult, names, values, false);
        }

        public static Expression CreateResult(Type tResult, IList<string> names, IList<Expression> values, bool named)
        {
            if (tResult.IsDictionary())
            {
                var constructor = tResult.GetConstructor(new[] { typeof(StringComparer) })
                    ?? typeof(Dictionary<,>).MakeGenericType(tResult.GetGenericArguments()).GetConstructor(new[] { typeof(StringComparer) });

                return CreateDictionaryInit(Expression.New(constructor, Expression.Constant(StringComparer.OrdinalIgnoreCase)), tResult, names, values);
            }

            if (values.Count == 1 && !tResult.IsTuple() && !(named && tResult == typeof(object)))
            {
                return Converted(values[0], tResult);
            }

            if (tResult == typeof(object))
            {
                return CreateDictionaryInit(Expression.New(typeof(ExpandoObject)), typeof(IDictionary<string, object>), names, values);
            }

            if (tResult.HasEmptyConstructor())
            {
                var bindings = names.Select((name, i) => Expression.Bind(tResult.GetPropertyOrField(name), Converted(values[i], tResult.GetPropertyOrFieldType(name))));

                return Expression.MemberInit(Expression.New(tResult), bindings);
            }

            var types = names.Select((name, i) => tResult.GetFieldType(name, i)).ToArray();
            var create = types.All(t => t != null) ? tResult.GetConstructor(types) : null;
            if (create == null)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} has no constructor that takes the {1} columns of the select list.", tResult.Name, names.Count), SqlErrorKind.Semantic);
            }

            return Expression.New(create, values.Select((value, i) => Converted(value, types[i])));
        }

        private static ListInitExpression CreateDictionaryInit(NewExpression create, Type dictionaryType, IList<string> names, IList<Expression> values)
        {
            var add = dictionaryType.GetMethod("Add", dictionaryType.GetGenericArguments());
            var valueType = dictionaryType.GetDictionaryValueType();

            return Expression.ListInit(create, names.Select((name, i) => Expression.ElementInit(add, Expression.Constant(name), Converted(values[i], valueType))));
        }

        private static Expression Converted(Expression value, Type type)
        {
            return value.Type == type ? value : ConvertTo(value, type);
        }

        public static Expression CreateFieldSelector(Expression item, string propertyName)
        {
            // source type is dynamic
            if (item.Type == typeof(object))
            {
                return Expression.Dynamic(new DynamicGetMemberBinder(propertyName), item.Type, item);
            }

            if (item.Type == typeof(ExpandoObject))
            {
                return Expression.Dynamic(new DynamicGetMemberBinder(propertyName), typeof(object), item);
            }

            // source type is dictionary
            var indexerMethod = item.Type.GetMethod("get_Item", new Type[] { typeof(string) });
            if (indexerMethod != null)
            {
                return Expression.Call(item, indexerMethod, Expression.Constant(propertyName));
            }

            // source type is a class or stuct
            var member = item.Type.GetPropertyOrField(propertyName);
            if (member == null)
            {
                var unknown = new ArgumentException(string.Format(CultureInfo.CurrentCulture, "'{0}' is not a member of type '{1}'", propertyName, item.Type), nameof(propertyName));
                unknown.Data[nameof(SqlErrorKind)] = SqlErrorKind.UnknownName;

                throw unknown;
            }

            return Expression.MakeMemberAccess(item, member);
        }

        public static Expression CreateFieldSelector(ParameterExpression item, string propertyName, Type returnType)
        {
            var selector = CreateFieldSelector(item, propertyName);

            if (selector.Type != returnType)
            {
                selector = Expression.Convert(selector, returnType);
            }

            return selector;
        }

        internal static ConstantExpression Nothing(Type type)
        {
            return Expression.Constant(type.IsValueType && Nullable.GetUnderlyingType(type) == null ? Activator.CreateInstance(type) : null, type);
        }

        internal static Expression ToKey(Expression value, Type key)
        {
            var underlying = Nullable.GetUnderlyingType(key);
            if (value.Type == key)
            {
                return value;
            }

            return underlying != null && Nullable.GetUnderlyingType(value.Type) == null && value.Type != underlying
                ? Expression.Convert(Expression.Convert(value, underlying), key)
                : Expression.Convert(value, key);
        }

        public static Expression ConvertTo(Expression expression, Type type)
        {
            if (expression.Type == typeof(object) && type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                return Expression.Convert(Expression.Call(typeof(ExpressionFactory), nameof(ChangeTypeExact), null, expression, Expression.Constant(type)), type);
            }

            var underlying = Nullable.GetUnderlyingType(type);
            if (expression.Type == typeof(object) && underlying != null)
            {
                return Expression.Convert(Expression.Call(typeof(ExpressionFactory), nameof(ChangeTypeExactOrNull), null, expression, Expression.Constant(underlying)), type);
            }

            return Expression.Convert(expression, type);
        }

        private static object ChangeTypeExactOrNull(object value, Type type)
        {
            return value == null ? null : ChangeTypeExact(value, type);
        }

        private static object ChangeTypeExact(object value, Type type)
        {
            var result = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            if (!Convert.ChangeType(result, value.GetType(), CultureInfo.InvariantCulture).Equals(value))
            {
                throw new InvalidCastException(string.Format(CultureInfo.CurrentCulture, "{0} cannot be converted to {1} without loss", value, type.Name));
            }

            return result;
        }

        public static LambdaExpression CreateFieldSelectorLambda(Type itemType, string propertyName)
        {
            Debug.Assert(!string.IsNullOrEmpty(propertyName));

            ParameterExpression item = Expression.Parameter(itemType, "item");

            return Expression.Lambda(CreateFieldSelector(item, propertyName), item);
        }

        public static Expression CreateFieldSelectorLambda(ParameterExpression item, string propertyName, Type returnType)
        {
            Debug.Assert(!string.IsNullOrEmpty(propertyName));

            return Expression.Lambda(CreateFieldSelector(item, propertyName, returnType), item);
        }
    }
}
