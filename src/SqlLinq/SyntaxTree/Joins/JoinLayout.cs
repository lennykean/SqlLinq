using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlLinq.SyntaxTree.Joins
{
    internal sealed class JoinLayout
    {
        private readonly List<(HashSet<string> Names, Type Type, bool Optional)> _sources = new List<(HashSet<string> Names, Type Type, bool Optional)>();

        internal JoinLayout(HashSet<string> names, IEnumerable table)
        {
            Rows = Add(names, table);
            RowType = _sources[0].Type;
        }

        internal Expression Rows { get; private set; }

        internal Type RowType { get; private set; }

        internal void Cross(HashSet<string> names, IEnumerable table)
        {
            var items = Add(names, table);
            var itemType = _sources[_sources.Count - 1].Type;
            var rowType = Composite(RowType, itemType);
            var row = Expression.Parameter(RowType, "row");
            var item = Expression.Parameter(itemType, "item");

            Rows = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.SelectMany),
                new[] { RowType, itemType, rowType },
                Rows,
                Expression.Lambda(items, row),
                Expression.Lambda(Pair(rowType, row, item), row, item));
            RowType = rowType;
        }

        internal void Join(Join join, HashSet<string> names, IEnumerable table, int outer, string outerField, string innerField)
        {
            var items = Add(names, table);
            var count = _sources.Count;
            var itemType = _sources[count - 1].Type;
            var rowType = Composite(RowType, itemType);
            var row = Expression.Parameter(RowType, "row");
            var item = Expression.Parameter(itemType, "item");
            var outerValue = outer < 0 ? Unqualified(row, SqlSyntax.Name(join.Children.OfType<TerminalNode>().First(t => t.Symbol.Name == "Id" && SqlSyntax.Unbracket(t.Text) == outerField)), count - 1) : Read(Item(row, outer, count - 1), outer, outerField);
            var (outerKey, innerKey) = Keys(outerValue, Read(item, count - 1, innerField));
            var outerSelector = Expression.Lambda(outerKey, row);
            var innerSelector = Expression.Lambda(innerKey, item);

            if (join is Left)
            {
                var matchesType = typeof(IEnumerable<>).MakeGenericType(itemType);
                var groupedType = Composite(RowType, matchesType);
                var matches = Expression.Parameter(matchesType, "matches");
                var grouped = Expression.Parameter(groupedType, "grouped");
                var match = Expression.Parameter(itemType, "match");
                var groupJoin = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.GroupJoin),
                    new[] { RowType, itemType, outerKey.Type, groupedType },
                    Rows,
                    items,
                    outerSelector,
                    innerSelector,
                    Expression.Lambda(Pair(groupedType, row, matches), row, matches));

                Rows = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.SelectMany),
                    new[] { groupedType, itemType, rowType },
                    groupJoin,
                    Expression.Lambda(Expression.Call(typeof(Enumerable), nameof(Enumerable.DefaultIfEmpty), new[] { itemType }, Expression.Property(grouped, "Inner")), grouped),
                    Expression.Lambda(Pair(rowType, Expression.Property(grouped, "Outer"), match), grouped, match));
                Optional(count - 1);
            }
            else if (join is Right)
            {
                var matchesType = typeof(IEnumerable<>).MakeGenericType(RowType);
                var groupedType = Composite(matchesType, itemType);
                var matches = Expression.Parameter(matchesType, "matches");
                var grouped = Expression.Parameter(groupedType, "grouped");
                var match = Expression.Parameter(RowType, "match");
                var groupJoin = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.GroupJoin),
                    new[] { itemType, RowType, outerKey.Type, groupedType },
                    items,
                    Rows,
                    innerSelector,
                    outerSelector,
                    Expression.Lambda(Pair(groupedType, matches, item), item, matches));

                Rows = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.SelectMany),
                    new[] { groupedType, RowType, rowType },
                    groupJoin,
                    Expression.Lambda(Expression.Call(typeof(Enumerable), nameof(Enumerable.DefaultIfEmpty), new[] { RowType }, Expression.Property(grouped, "Outer"), Empty(RowType, count - 1)), grouped),
                    Expression.Lambda(Pair(rowType, match, Expression.Property(grouped, "Inner")), grouped, match));
                for (var i = 0; i < count - 1; i++)
                {
                    Optional(i);
                }
            }
            else
            {
                Rows = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Join),
                    new[] { RowType, itemType, outerKey.Type, rowType },
                    Rows,
                    items,
                    outerSelector,
                    innerSelector,
                    Expression.Lambda(Pair(rowType, row, item), row, item));
            }

            RowType = rowType;
        }

        internal Expression Read(Expression row, Identifier id)
        {
            var index = id.Qualifier.Length == 0 ? -1 : _sources.FindIndex(s => s.Names.Contains(id.Qualifier));
            try
            {
                return index < 0 ? Unqualified(row, id, _sources.Count) : Read(Item(row, index, _sources.Count), index, id.LocalId);
            }
            catch (SqlException e) when (e.Start < 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, id.Start, id.Length);
            }
        }

        internal string Field(Identifier id)
        {
            var index = id.Qualifier.Length == 0
                ? _sources.TakeWhile(s => !Untyped(s.Type)).ToList().FindIndex(s => s.Type.HasPropertyOrField(id.LookupId))
                : _sources.FindIndex(s => s.Names.Contains(id.Qualifier));

            return index < 0 ? id.LookupId : index + "." + id.LocalId;
        }

        internal Expression Star(Expression row)
        {
            var count = _sources.Count;
            if (_sources.Any(s => Untyped(s.Type)))
            {
                var items = Enumerable.Range(0, count).Select(i => Expression.Convert(Item(row, i, count), typeof(object)));

                return Expression.Call(typeof(JoinLayout), nameof(Flatten), null, Expression.NewArrayInit(typeof(object), items), Expression.Constant(this));
            }

            var add = typeof(Dictionary<string, object>).GetMethod(nameof(Dictionary<string, object>.Add));
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fields = new List<ElementInit>();
            for (var i = 0; i < count; i++)
            {
                var item = Item(row, i, count);
                foreach (var field in _sources[i].Type.ColumnNames())
                {
                    var value = Expression.Convert(Read(item, i, field), typeof(object));
                    foreach (var key in _sources[i].Names.Select(name => name + "." + field).Concat(new[] { field }).Where(keys.Add))
                    {
                        fields.Add(Expression.ElementInit(add, Expression.Constant(key), value));
                    }
                }
            }

            var create = Expression.New(typeof(Dictionary<string, object>).GetConstructor(new[] { typeof(IEqualityComparer<string>) }), Expression.Constant(StringComparer.OrdinalIgnoreCase, typeof(IEqualityComparer<string>)));

            return fields.Count == 0 ? (Expression)create : Expression.ListInit(create, fields);
        }

        private Expression Add(HashSet<string> names, IEnumerable table)
        {
            var type = SourcesExtensions.ElementType(table);
            _sources.Add((names, type, false));

            return SourcesExtensions.Sequence(table, type);
        }

        private void Optional(int index)
        {
            var source = _sources[index];
            _sources[index] = (source.Names, source.Type, true);
        }

        private Expression Read(Expression item, int index, string field)
        {
            if (field == null)
            {
                return item;
            }

            var source = _sources[index];
            if (Untyped(source.Type))
            {
                return Expression.Call(typeof(JoinLayout), nameof(UntypedField), null, Expression.Convert(item, typeof(object)), Expression.Constant(field));
            }

            if (!source.Type.HasPropertyOrField(field))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be read because it isn't a field of {1}.", field, source.Type.Name), SqlErrorKind.UnknownName);
            }

            var value = ExpressionFactory.CreateFieldSelector(item, field);
            if (!source.Optional || item.Type.IsValueType)
            {
                return value;
            }

            var type = value.Type.IsValueType && Nullable.GetUnderlyingType(value.Type) == null ? typeof(Nullable<>).MakeGenericType(value.Type) : value.Type;

            return Expression.Condition(
                Expression.Equal(item, Expression.Constant(null, item.Type)),
                ExpressionFactory.Nothing(type),
                value.Type == type ? value : Expression.Convert(value, type));
        }

        private Expression Unqualified(Expression row, Identifier id, int count)
        {
            var dotted = id.Qualifier.Length > 0;
            if (!dotted && _sources.Count(s => !Untyped(s.Type) && s.Type.HasPropertyOrField(id.LookupId)) > 1)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be resolved because more than one joined table has it.", id.LookupId), SqlErrorKind.Semantic, id.Start, id.Length);
            }

            Expression result = null;
            for (var i = count - 1; i >= 0; i--)
            {
                var item = Item(row, i, count);
                var type = _sources[i].Type;
                if (!Untyped(type))
                {
                    if (!dotted && type.HasPropertyOrField(id.LookupId))
                    {
                        result = Read(item, i, id.LookupId);
                    }

                    continue;
                }

                result = Expression.Condition(
                    Expression.Call(typeof(JoinLayout), nameof(Claims), null, Expression.Convert(item, typeof(object)), Expression.Constant(id.LookupId)),
                    Read(item, i, id.LookupId),
                    result == null ? Missing(id) : Expression.Convert(result, typeof(object)));
            }

            if (result != null)
            {
                return result;
            }

            throw new SqlException(dotted
                ? string.Format(CultureInfo.CurrentCulture, "{1} can't be read because {0} isn't a source or alias of this query.", id.Qualifier, id.LookupId)
                : string.Format(CultureInfo.CurrentCulture, "{0} can't be read because it isn't a field of any source of this query.", id.LookupId), SqlErrorKind.UnknownName, id.Start, id.Length);
        }

        private static UnaryExpression Missing(Identifier id)
        {
            var error = id.Qualifier.Length > 0
                ? Expression.New(typeof(SqlException).GetConstructor(new[] { typeof(string), typeof(SqlErrorKind) }), Expression.Constant(string.Format(CultureInfo.CurrentCulture, "{1} can't be read because {0} isn't a source or alias of this query, or a field of the row.", id.Qualifier, id.LookupId)), Expression.Constant(SqlErrorKind.UnknownName))
                : Expression.New(typeof(KeyNotFoundException).GetConstructor(new[] { typeof(string) }), Expression.Constant(string.Format(CultureInfo.CurrentCulture, "The given key '{0}' was not present in the dictionary.", id.LookupId)));

            return Expression.Throw(error, typeof(object));
        }

        private static Expression Item(Expression row, int index, int count)
        {
            var path = row;
            for (var i = count - 1; i > index; i--)
            {
                path = Expression.Property(path, "Outer");
            }

            return index == 0 ? path : Expression.Property(path, "Inner");
        }

        private static Expression Empty(Type type, int count)
        {
            if (count == 1)
            {
                return ExpressionFactory.Nothing(type);
            }

            var parts = type.GetGenericArguments();

            return Pair(type, Empty(parts[0], count - 1), ExpressionFactory.Nothing(parts[1]));
        }

        private static Type Composite(Type outer, Type inner)
        {
            return typeof(JoinedRow<,>).MakeGenericType(outer, inner);
        }

        private static NewExpression Pair(Type type, Expression outer, Expression inner)
        {
            return Expression.New(
                type.GetConstructors()[0],
                new[] { outer, inner },
                type.GetProperty("Outer"),
                type.GetProperty("Inner"));
        }

        private static (Expression Outer, Expression Inner) Keys(Expression outer, Expression inner)
        {
            var key = outer.Type.KeyType(inner.Type);

            return key == null ? (KeyOf(outer), KeyOf(inner)) : (ExpressionFactory.ToKey(outer, key), ExpressionFactory.ToKey(inner, key));
        }

        private static MethodCallExpression KeyOf(Expression value)
        {
            return Expression.Call(typeof(SourcesExtensions), nameof(SourcesExtensions.KeyOf), null, Expression.Convert(value, typeof(object)));
        }

        private static bool Untyped(Type type)
        {
            return type == typeof(object) || type.IsDictionary();
        }

        private static bool Claims(object item, string name)
        {
            if (item is IDictionary<string, object> dictionary)
            {
                return dictionary.ContainsKey(name) || dictionary.Keys.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            return item == null || item.GetType().HasPropertyOrField(name);
        }

        private static object UntypedField(object item, string name)
        {
            if (item == null)
            {
                return null;
            }

            if (item is IDictionary<string, object> dictionary)
            {
                return dictionary.TryGetValue(name, out var value)
                    ? value
                    : dictionary.FirstOrDefault(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var property = item.GetType().GetProperty(name, flags);
            if (property != null)
            {
                return property.GetValue(item);
            }

            var field = item.GetType().GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(item);
            }

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be read because it isn't a field of {1}.", name, item.GetType().Name), SqlErrorKind.UnknownName);
        }

        private static Dictionary<string, object> Flatten(object[] items, JoinLayout layout)
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Length; i++)
            {
                var source = layout._sources[i];
                foreach (var field in Fields(items[i], source.Type))
                {
                    var value = UntypedField(items[i], field);
                    foreach (var name in source.Names)
                    {
                        row[name + "." + field] = value;
                    }

                    if (!row.ContainsKey(field))
                    {
                        row[field] = value;
                    }
                }
            }

            return row;
        }

        private static IEnumerable<string> Fields(object item, Type type)
        {
            if (item is IDictionary<string, object> dictionary)
            {
                return dictionary.Keys;
            }

            if (!Untyped(type))
            {
                return type.ColumnNames();
            }

            return item == null ? Enumerable.Empty<string>() : item.GetType().ColumnNames();
        }
    }
}
