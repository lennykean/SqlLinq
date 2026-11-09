using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Clauses;
using SqlLinq.SyntaxTree.Joins;

namespace SqlLinq
{
    public static class SourcesExtensions
    {
        public static IEnumerable<IDictionary<string, object>> Query(
            this IDictionary<string, IEnumerable> sources,
            string sql,
            CancellationToken cancellationToken = default)
        {
            return sources.Query<IDictionary<string, object>>(sql, null, cancellationToken);
        }

        public static IEnumerable<IDictionary<string, object>> Query(
            this IDictionary<string, IEnumerable> sources,
            string sql,
            IEnumerable<ISqlFunction> functions,
            CancellationToken cancellationToken = default)
        {
            return sources.Query<IDictionary<string, object>>(sql, functions, cancellationToken);
        }

        public static IEnumerable<TResult> Query<TResult>(
            this IDictionary<string, IEnumerable> sources,
            string sql,
            CancellationToken cancellationToken = default)
        {
            return sources.Query<TResult>(sql, null, cancellationToken);
        }

        public static IEnumerable<TResult> Query<TResult>(
            this IDictionary<string, IEnumerable> sources,
            string sql,
            IEnumerable<ISqlFunction> functions,
            CancellationToken cancellationToken = default)
        {
            return Run<object, TResult>(sources, sql, functions, false, true, cancellationToken);
        }

        internal static IEnumerable<TResult> Run<TSource, TResult>(
            IDictionary<string, IEnumerable> sources,
            string sql,
            IEnumerable<ISqlFunction> functions,
            bool requireJoin,
            bool elementType = false,
            CancellationToken cancellationToken = default)
        {
            var query = Tree<TSource, TResult>(sources, sql, functions, requireJoin, elementType, cancellationToken).Compile();
            cancellationToken.ThrowIfCancellationRequested();
            return query();
        }

        internal static Expression<Func<IEnumerable<TResult>>> Tree<TResult>(IDictionary<string, IEnumerable> sources, string sql)
        {
            return Tree<object, TResult>(sources, sql, null, false, true, default);
        }

        private static Expression<Func<IEnumerable<TResult>>> Tree<TSource, TResult>(
            IDictionary<string, IEnumerable> sources,
            string sql,
            IEnumerable<ISqlFunction> functions,
            bool requireJoin,
            bool elementType,
            CancellationToken cancellationToken)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (cancellationToken.CanBeCanceled)
            {
                sources = Cancellable(sources, cancellationToken);
            }

            var parser = new SqlParser(functions);
            if (!parser.Parse(sql))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin statement\n\t{1}", parser.ErrorString, parser.ErrorLine));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var body = parser.SyntaxTree is UnionNode union
                ? union.Combine<TResult>(union.Parts.Select(p => RunSelect<TSource, TResult>(sources, p.Select, sql, requireJoin, elementType)).ToList())
                : RunSelect<TSource, TResult>(sources, parser.SyntaxTree as SelectNode, sql, requireJoin, elementType);

            cancellationToken.ThrowIfCancellationRequested();
            return Expression.Lambda<Func<IEnumerable<TResult>>>(body);
        }

        private static Expression RunSelect<TSource, TResult>(
            IDictionary<string, IEnumerable> sources,
            SelectNode select,
            string sql,
            bool requireJoin,
            bool elementType)
        {
            var members = select?.FromClause == null ? new List<(string Name, string Alias)>() : select.FromClause.Members;
            var joins = select?.FromClause == null ? new List<Join>() : select.FromClause.FindDescendants<Join>().ToList();

            if (requireJoin && joins.Count == 0)
            {
                throw new SqlException("The query does not contain a JOIN expression\n" + sql);
            }

            if (joins.Count == 0 && (members.Count <= 1 || sources.Count == 1))
            {
                var source = members.Count == 0 ? sources.First() : Resolve(sources, members[0], "this", sources.Count == 1);
                if (members.Count == 0 || members[0].Alias == null)
                {
                    select.Names.Add(source.Key);
                }

                Scope(sources, select, source.Key);
                var type = elementType ? ElementType(source.Value) : typeof(TSource);

                return QueryTree.Build(select, Sequence(source.Value, type), type, typeof(TResult));
            }

            select.Layout = Layout(sources, members, joins, select.Names);
            select.Joined = true;
            Scope(sources, select, null);

            return QueryTree.Build(select, select.Layout.Rows, select.Layout.RowType, typeof(TResult));
        }

        private static void Scope(IDictionary<string, IEnumerable> sources, SelectNode select, string key)
        {
            foreach (var subquery in select.FindDescendants<SelectNode>().Where(s => s.EnclosingSelect() == select))
            {
                var table = Resolve(sources, subquery.FromClause.Members[0], "this", sources.Count == 1);
                if (subquery.FromClause.Members[0].Alias == null)
                {
                    subquery.Names.Add(table.Key);
                }

                var wholeTable = select.Columns.FindDescendants<SelectNode>().Contains(subquery)
                    || select.HavingClause != null && select.HavingClause.FindDescendants<SelectNode>().Contains(subquery);
                if (table.Key != key || wholeTable)
                {
                    subquery.Source = table.Value;
                }

                Scope(sources, subquery, table.Key);
            }
        }

        internal static Expression Sequence(IEnumerable table, Type type)
        {
            var sequence = typeof(IEnumerable<>).MakeGenericType(type);

            return sequence.IsInstanceOfType(table)
                ? (Expression)Expression.Constant(table, sequence)
                : Expression.Call(typeof(Enumerable), nameof(Enumerable.Cast), new[] { type }, Expression.Constant(table ?? Array.Empty<object>(), typeof(IEnumerable)));
        }

        internal static Type ElementType(IEnumerable source)
        {
            var type = source?.GetType() ?? typeof(object);
            var enumerable = type.GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable?.GetGenericArguments()[0] ?? typeof(object);
        }

        private static Dictionary<string, IEnumerable> Cancellable(IDictionary<string, IEnumerable> sources, CancellationToken cancellationToken)
        {
            var wrap = typeof(SourcesExtensions).GetMethod(nameof(Rows), BindingFlags.NonPublic | BindingFlags.Static);
            return sources.ToDictionary(
                s => s.Key,
                s => s.Value == null ? null : (IEnumerable)wrap.MakeGenericMethod(ElementType(s.Value)).Invoke(null, new object[] { s.Value, cancellationToken }));
        }

        private static IEnumerable<T> Rows<T>(IEnumerable table, CancellationToken cancellationToken)
        {
            foreach (var row in table as IEnumerable<T> ?? table.Cast<T>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return row;
            }
        }

        private static JoinLayout Layout(
            IDictionary<string, IEnumerable> sources,
            List<(string Name, string Alias)> members,
            List<Join> joins,
            HashSet<string> scope)
        {
            var first = members[0];
            var source = Resolve(sources, first, "this", false);
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { first.Alias ?? first.Name };
            var names = new List<HashSet<string>> { Names(source.Key, first) };
            var layout = new JoinLayout(names[0], source.Value);

            foreach (var member in members.Skip(1))
            {
                var crossed = Add(sources, member, aliases, names);
                layout.Cross(names[names.Count - 1], crossed);
            }

            foreach (var join in joins)
            {
                var joinedMembers = FromClause.Of(join.Children.OfType<NonTerminalNode>().First());
                if (joinedMembers.Count != 1)
                {
                    throw new SqlException("JOIN takes one source. Use another JOIN for each other source.");
                }

                var joined = Add(sources, joinedMembers[0], aliases, names);
                var (outer, outerField, innerField) = Keys(join, names);
                layout.Join(join, names[names.Count - 1], joined, outer, outerField, innerField);
            }

            foreach (var name in names)
            {
                scope.UnionWith(name);
            }

            return layout;
        }

        private static IEnumerable Add(
            IDictionary<string, IEnumerable> sources,
            (string Name, string Alias) member,
            HashSet<string> aliases,
            List<HashSet<string>> names)
        {
            if (!aliases.Add(member.Alias ?? member.Name))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} is joined more than once. Give each join its own alias.", member.Alias ?? member.Name));
            }

            var joined = Resolve(sources, member, "that", false);
            names.Add(Names(joined.Key, member));

            return joined.Value;
        }

        private static KeyValuePair<string, IEnumerable> Resolve(IDictionary<string, IEnumerable> sources, (string Name, string Alias) member, string fallback, bool onlyOne)
        {
            var source = sources.FirstOrDefault(s => s.Key.Equals(member.Alias, StringComparison.OrdinalIgnoreCase) || s.Key.Equals(member.Name, StringComparison.OrdinalIgnoreCase));
            if (source.Key != null)
            {
                return source;
            }

            if (onlyOne)
            {
                return sources.First();
            }

            source = sources.FirstOrDefault(s => s.Key.Equals(fallback, StringComparison.OrdinalIgnoreCase));
            if (source.Key != null)
            {
                return source;
            }

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' isn't one of the sources: {1}.", member.Name, string.Join(", ", sources.Keys)));
        }

        private static HashSet<string> Names(string key, (string Name, string Alias) member)
        {
            return member.Alias == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key, member.Name }
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { member.Alias };
        }

        private static (int Outer, string OuterField, string InnerField) Keys(Join join, List<HashSet<string>> names)
        {
            var on = join.Children.OfType<TerminalNode>().Where(t => t.Symbol.Name == "Id").Select(t => t.Text).ToList();
            var left = Side(on[0], names);
            var right = Side(on[1], names);
            var inner = names[names.Count - 1];
            var earlier = names.Take(names.Count - 1).ToList();
            var leftInner = left.Alias != null && inner.Contains(left.Alias);
            var rightInner = right.Alias != null && inner.Contains(right.Alias);

            if (leftInner != rightInner)
            {
                var (innerSide, outerSide) = leftInner ? (left, right) : (right, left);
                var outer = outerSide.Alias == null ? 0 : earlier.FindIndex(n => n.Contains(outerSide.Alias));
                if (outer >= 0)
                {
                    return (outer, outerSide.Field, innerSide.Field);
                }
            }

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "JOIN ON {0} = {1} must compare a field of the joined source with a field of an earlier source.", on[0], on[1]));
        }

        private static (string Alias, string Field) Side(string text, List<HashSet<string>> names)
        {
            var dot = text.LastIndexOf('.');
            if (dot >= 0)
            {
                return (SqlSyntax.Unbracket(text.Substring(0, dot)), SqlSyntax.Unbracket(text.Substring(dot + 1)));
            }

            var name = SqlSyntax.Unbracket(text);

            return names.Any(n => n.Contains(name)) ? (name, (string)null) : ((string)null, name);
        }

        internal static object KeyOf(object value)
        {
            if (value == null)
            {
                return null;
            }

            var code = Type.GetTypeCode(value.GetType());

            return code >= TypeCode.SByte && code <= TypeCode.Decimal ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : value;
        }
    }
}
