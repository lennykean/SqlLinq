using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SqlLinq
{
    static class TypeExtensions
    {
        public static Type GetDictionaryValueType(this Type dictionaryType)
        {
            Debug.Assert(dictionaryType.IsDictionary());

            var dictionary = dictionaryType.IsGenericType ? dictionaryType : dictionaryType.GetInterface(typeof(IDictionary<,>).Name);

            return dictionary.GetGenericArguments()[1];
        }

        public static IEnumerable<string> ColumnNames(this Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Concat(type.IsInterface ? type.GetInterfaces().SelectMany(i => i.GetProperties()) : Enumerable.Empty<PropertyInfo>())
                .Where(p => p.GetIndexParameters().Length == 0)
                .Select(p => p.Name)
                .Distinct()
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name));
        }

        public static Type GetFieldType(this Type t, string field)
        {
            if (t.IsDictionary())
                return t.GetDictionaryValueType();

            if (t == typeof(object))
            {
                return typeof(object);
            }

            if (t.HasPropertyOrField(field))
                return t.GetPropertyOrFieldType(field);

            return null;
        }

        public static Type GetFieldType(this Type t, string field, int index)
        {
            Type ret = t.GetFieldType(field);
            if (ret != null)
                return ret;

            if (t.IsTuple())
                return t.GetGenericArguments()[index];

            return null;
        }

        private static bool IsCompatibleWithGenericTypeDefinition(this Type type, Type def)
        {
            Debug.Assert(def.IsGenericTypeDefinition);

            return type.GetInterface(def.Name) != null || type.Name.Contains(def.Name);
        }

        public static bool IsTuple(this Type type)
        {
            return type.FullName.StartsWith("System.Tuple", StringComparison.CurrentCulture);
        }

        public static bool IsDictionary(this Type type)
        {
            return type.IsCompatibleWithGenericTypeDefinition(typeof(IDictionary<,>));
        }

        public static bool HasEmptyConstructor(this Type type)
        {
            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        public static bool HasPropertyOrField(this Type type, string name)
        {
            return type.GetPropertyOrField(name) != null;
        }

        public static MemberInfo GetPropertyOrField(this Type type, string name)
        {
            MemberInfo member = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return (member ?? type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)) ?? type.GetInterfaces().Select(i => i.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)).FirstOrDefault(p => p != null);
        }

        public static Type GetPropertyOrFieldType(this Type type, string name)
        {
            PropertyInfo p = type.GetPropertyOrField(name) as PropertyInfo;
            if (p != null)
                return p.PropertyType;

            FieldInfo f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Debug.Assert(f != null);
            return f.FieldType;
        }

        public static object GetPropertyOrFieldValue(this object o, string name)
        {
            Debug.Assert(o != null);

            // look for a property
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (p != null)
                return p.GetValue(o, null);

            // and then a field
            FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            Debug.Assert(f != null);
            return f.GetValue(o);
        }

        private static Type WidestNullable(this Type type, Type other)
        {
            if (type == typeof(double?) || other == typeof(double?))
                return typeof(double?);

            if (type == typeof(long?) || other == typeof(long?))
                return typeof(long?);

            if (type == typeof(ulong?) || other == typeof(ulong?))
                return typeof(ulong?);

            if (type == typeof(float?) || other == typeof(float?))
                return typeof(float?);

            if (type == typeof(int?) || other == typeof(int?))
                return typeof(int?);

            if (type == typeof(uint?) || other == typeof(uint?))
                return typeof(uint?);

            if (type == typeof(short?) || other == typeof(short?))
                return typeof(short?);

            if (type == typeof(ushort?) || other == typeof(ushort?))
                return typeof(ushort?);

            if (type == typeof(sbyte?) || other == typeof(sbyte?))
                return typeof(sbyte?);

            if (type == typeof(byte?) || other == typeof(byte?))
                return typeof(byte?);

            if (type == typeof(bool?) || other == typeof(bool?))
                return typeof(bool?);

            if (other == typeof(object))
                return type;

            return null;
        }

        internal static Type KeyType(this Type type, Type other)
        {
            var left = Nullable.GetUnderlyingType(type) ?? type;
            var right = Nullable.GetUnderlyingType(other) ?? other;
            if (left.IsValueType && right.IsValueType)
            {
                var common = left.Widest(right);

                return common != null && common.IsValueType ? typeof(Nullable<>).MakeGenericType(Nullable.GetUnderlyingType(common) ?? common) : null;
            }

            return left == right && left != typeof(object) ? left : null;
        }

        internal static Type Lifted(this Type widest, params Type[] types)
        {
            var nullable = types.Any(t => Nullable.GetUnderlyingType(t) != null || t == typeof(object));

            return nullable && widest.IsValueType && Nullable.GetUnderlyingType(widest) == null ? typeof(Nullable<>).MakeGenericType(widest) : widest;
        }

        public static Type Widest(this Type type, Type other)
        {
            if (type == other && !type.PromotesToInt())
                return type;

            if (type == typeof(string) || other == typeof(string))
                return typeof(string);

            if (type == typeof(object))
            {
                return other.Widest(type);
            }

            if (other == typeof(object) && type.IsPrimitive && type != typeof(bool) && type != typeof(char))
                return typeof(double);

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null && other == typeof(object))
            {
                var boxed = underlying.Widest(other);

                return boxed != null && boxed.IsValueType ? typeof(Nullable<>).MakeGenericType(boxed) : type;
            }

            var otherUnderlying = Nullable.GetUnderlyingType(other);
            if ((underlying != null || otherUnderlying != null) && type.IsValueType && other.IsValueType)
            {
                var widest = (underlying ?? type).Widest(otherUnderlying ?? other);

                return widest != null && underlying != null && otherUnderlying != null ? typeof(Nullable<>).MakeGenericType(widest) : widest;
            }

            if (!type.IsValueType)
                return type.WidestNullable(other);

            if (type == typeof(double) || other == typeof(double))
                return typeof(double);

            if (type == typeof(decimal) || other == typeof(decimal))
            {
                return typeof(decimal);
            }

            if (type == typeof(float) || other == typeof(float))
                return typeof(float);

            if ((type == typeof(ulong) || other == typeof(ulong)) && (type == typeof(long) || other == typeof(long) || type == typeof(int) || other == typeof(int) || type == typeof(short) || other == typeof(short) || type == typeof(sbyte) || other == typeof(sbyte)))
                return typeof(decimal);

            if (type == typeof(long) || other == typeof(long))
                return typeof(long);

            if (type == typeof(ulong) || other == typeof(ulong))
                return typeof(ulong);

            if (type == typeof(uint) || other == typeof(uint))
                return type == typeof(int) || other == typeof(int) || type == typeof(short) || other == typeof(short) || type == typeof(sbyte) || other == typeof(sbyte) ? typeof(long) : typeof(uint);

            if (type == typeof(int) || other == typeof(int) || type.PromotesToInt() || other.PromotesToInt())
                return typeof(int);

            if (type == typeof(bool) || other == typeof(bool))
                return typeof(bool);

            if (other == typeof(object))
                return type;

            return null;
        }

        private static bool PromotesToInt(this Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            return underlying == typeof(sbyte) || underlying == typeof(byte) || underlying == typeof(short) || underlying == typeof(ushort) || underlying == typeof(char);
        }

        public static Type Widest(this Type type, Type other, Type third)
        {
            Type widest = type.Widest(other);
            if (widest != null)
                return widest.Widest(third);

            return null;
        }
    }
}
