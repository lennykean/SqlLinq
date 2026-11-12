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
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var syntax = SqlSyntaxTree.Parse(sql, functions);
            if (syntax.Error != null)
            {
                throw syntax.Error;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var plan = Compile<TSource, TResult>(syntax, Shape(sources), requireJoin, elementType);
            cancellationToken.ThrowIfCancellationRequested();

            return plan.Execute(sources, cancellationToken);
        }

        internal static Expression<Func<IEnumerable<TResult>>> Tree<TResult>(IDictionary<string, IEnumerable> sources, string sql)
        {
            var syntax = SqlSyntaxTree.Parse(sql);
            if (syntax.Error != null)
            {
                throw syntax.Error;
            }

            var plan = Plan<object, TResult>(syntax, Shape(sources), false, true, false, out _);
            var tables = Expression.Constant(sources.Values.Select(t => t ?? Array.Empty<object>()).ToArray(), typeof(IEnumerable<IEnumerable>));

            return Expression.Lambda<Func<IEnumerable<TResult>>>(ParameterReplacer.Replace(plan.Body, plan.Parameters[0], tables));
        }

        internal static SqlPlan<TResult> Compile<TSource, TResult>(SqlSyntaxTree syntax, IReadOnlyDictionary<string, Type> tables, bool requireJoin, bool elementType, bool bare = false)
        {
            var plan = Plan<TSource, TResult>(syntax, tables, requireJoin, elementType, bare, out var root);
            var select = root as SelectNode ?? ((UnionNode)root).Parts[0].Select;
            var star = (tables.Count == 1 ? tables.Values.First() : null) ?? typeof(object);
            var columns = !(select.Columns is ColumnsStar)
                ? select.Columns.ColumnSources.Select(c => c.ColumnAlias).ToList()
                : star == typeof(object) || star.IsDictionary() ? new List<string>() : star.ColumnNames().ToList();

            return new SqlPlan<TResult>(syntax, tables.Keys.ToList(), columns, plan);
        }

        private static Expression<Func<IEnumerable<IEnumerable>, IEnumerable<TResult>>> Plan<TSource, TResult>(
            SqlSyntaxTree syntax,
            IReadOnlyDictionary<string, Type> tables,
            bool requireJoin,
            bool elementType,
            bool bare,
            out NonTerminalNode root)
        {
            if (syntax.Error != null)
            {
                throw syntax.Error;
            }

            if (!(syntax.Root is SelectNode) && !(syntax.Root is UnionNode))
            {
                throw new InvalidOperationException("The syntax tree is an expression; compile it with SqlExpression.Compile.");
            }

            var copy = syntax.Root.Copy();
            root = copy;
            var parameter = Expression.Parameter(typeof(IEnumerable<IEnumerable>), "tables");
            var sources = new Dictionary<string, IEnumerable>();
            foreach (var table in tables)
            {
                sources.Add(table.Key, table.Value == null ? null : new TableSlot(parameter, sources.Count, table.Value));
            }

            var body = bare ? Bare<TSource, TResult>((SelectNode)copy, parameter)
                : copy is UnionNode union
                ? union.Combine<TResult>(union.Parts.Select(p => QueryTree.Scoped(p.Select, parameter, () => RunSelect<TSource, TResult>(sources, p.Select, syntax.Text, requireJoin, elementType))).ToList())
                : QueryTree.Scoped((SelectNode)copy, parameter, () => RunSelect<TSource, TResult>(sources, (SelectNode)copy, syntax.Text, requireJoin, elementType));

            return Expression.Lambda<Func<IEnumerable<IEnumerable>, IEnumerable<TResult>>>(body, parameter);
        }

        private static InvocationExpression Bare<TSource, TResult>(SelectNode select, ParameterExpression tables)
        {
            var source = Expression.Parameter(typeof(IEnumerable<TSource>), "source");
            var query = Expression.Lambda(QueryTree.Build(select, source, typeof(TSource), typeof(TResult)), source);

            return Expression.Invoke(query, Expression.Convert(TableSlot.Table(tables, 0), source.Type));
        }

        private static Dictionary<string, Type> Shape(IDictionary<string, IEnumerable> sources)
        {
            return sources.ToDictionary(s => s.Key, s => s.Value == null ? null : ElementType(s.Value));
        }

        private static Expression RunSelect<TSource, TResult>(
            Dictionary<string, IEnumerable> sources,
            SelectNode select,
            string sql,
            bool requireJoin,
            bool elementType)
        {
            var members = select?.FromClause == null ? new List<(string Name, string Alias)>() : select.FromClause.Members;
            var joins = select?.FromClause == null ? new List<Join>() : select.FromClause.FindDescendants<Join>().ToList();

            if (requireJoin && joins.Count == 0)
            {
                throw new SqlException("The query does not contain a JOIN expression\n" + sql, SqlErrorKind.Semantic);
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

        private static void Scope(Dictionary<string, IEnumerable> sources, SelectNode select, string key)
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
            if (table is TableSlot slot)
            {
                return slot.Rows(type);
            }

            var sequence = typeof(IEnumerable<>).MakeGenericType(type);

            return sequence.IsInstanceOfType(table)
                ? (Expression)Expression.Constant(table, sequence)
                : Expression.Call(typeof(Enumerable), nameof(Enumerable.Cast), new[] { type }, Expression.Constant(table ?? Array.Empty<object>(), typeof(IEnumerable)));
        }

        internal static Type ElementType(IEnumerable source)
        {
            if (source is TableSlot slot)
            {
                return slot.ElementType;
            }

            var type = source?.GetType() ?? typeof(object);
            var enumerable = type.GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable?.GetGenericArguments()[0] ?? typeof(object);
        }

        internal static Dictionary<string, IEnumerable> Cancellable(IDictionary<string, IEnumerable> sources, CancellationToken cancellationToken)
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
            Dictionary<string, IEnumerable> sources,
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
                    throw new SqlException("JOIN takes one source. Use another JOIN for each other source.", SqlErrorKind.Semantic);
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
            Dictionary<string, IEnumerable> sources,
            (string Name, string Alias) member,
            HashSet<string> aliases,
            List<HashSet<string>> names)
        {
            if (!aliases.Add(member.Alias ?? member.Name))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} is joined more than once. Give each join its own alias.", member.Alias ?? member.Name), SqlErrorKind.Semantic);
            }

            var joined = Resolve(sources, member, "that", false);
            names.Add(Names(joined.Key, member));

            return joined.Value;
        }

        private static KeyValuePair<string, IEnumerable> Resolve(Dictionary<string, IEnumerable> sources, (string Name, string Alias) member, string fallback, bool onlyOne)
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

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "'{0}' isn't one of the sources: {1}.", member.Name, string.Join(", ", sources.Keys)), SqlErrorKind.UnknownName);
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

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "JOIN ON {0} = {1} must compare a field of the joined source with a field of an earlier source.", on[0], on[1]), SqlErrorKind.Semantic);
        }

        private static (string Alias, string Field) Side(string text, List<HashSet<string>> names)
        {
            var dot = text.EndsWith(']') ? text.LastIndexOf('[') - 1 : text.LastIndexOf('.');
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
