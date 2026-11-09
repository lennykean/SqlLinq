using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Clauses
{
    internal static class ResultOrder
    {
        private static readonly IComparer<object> ValueOrder = Comparer<object>.Create((left, right) => Comparer.Default.Compare(SourcesExtensions.KeyOf(left), SourcesExtensions.KeyOf(right)));

        internal static Expression Order(Expression rows, Type tResult, IList<(Func<Expression, Expression> Key, bool Descending)> keys)
        {
            var ordered = rows;
            for (var i = 0; i < keys.Count; i++)
            {
                var row = Expression.Parameter(tResult, "result");
                var key = keys[i].Key(row);
                var method = i == 0
                    ? keys[i].Descending ? nameof(Enumerable.OrderByDescending) : nameof(Enumerable.OrderBy)
                    : keys[i].Descending ? nameof(Enumerable.ThenByDescending) : nameof(Enumerable.ThenBy);
                var selector = Expression.Lambda(Expression.GetFuncType(tResult, key.Type), key, row);

                ordered = key.Type == typeof(object)
                    ? Expression.Call(typeof(Enumerable), method, new[] { tResult, key.Type }, ordered, selector, Expression.Constant(ValueOrder, typeof(IComparer<object>)))
                    : Expression.Call(typeof(Enumerable), method, new[] { tResult, key.Type }, ordered, selector);
            }

            return ordered;
        }

        internal static Expression Column(Expression row, string name, int index, bool single)
        {
            if (row.Type != typeof(object) && !row.Type.IsDictionary())
            {
                if (single)
                {
                    return row;
                }

                if (row.Type.IsTuple())
                {
                    return Expression.Property(row, "Item" + (index + 1).ToString(CultureInfo.InvariantCulture));
                }

                if (row.Type.HasPropertyOrField(name))
                {
                    return Expression.PropertyOrField(row, name);
                }
            }

            return Expression.Call(
                typeof(ResultOrder),
                nameof(Column),
                null,
                Expression.Convert(row, typeof(object)),
                Expression.Constant(name, typeof(string)),
                Expression.Constant(index),
                Expression.Constant(single));
        }

        internal static object Column(object row, string name, int index, bool single)
        {
            if (row is IDictionary<string, object> dictionary)
            {
                return dictionary.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            }

            if (single || row == null)
            {
                return row;
            }

            return row.GetType().IsTuple() ? row.GetPropertyOrFieldValue("Item" + (index + 1).ToString(CultureInfo.InvariantCulture)) : row.GetPropertyOrFieldValue(name);
        }
    }
}
