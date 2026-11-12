using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Clauses;

namespace SqlLinq.SyntaxTree
{
    [SyntaxNode(RuleConstants.RULE_QUERY_UNION)]
    [SyntaxNode(RuleConstants.RULE_QUERY_UNION_ALL)]
    internal sealed class UnionNode : NonTerminalNode
    {
        internal List<(SelectNode Select, bool All)> Parts
        {
            get
            {
                var parts = Children[0] is UnionNode union ? union.Parts : new List<(SelectNode Select, bool All)> { ((SelectNode)Children[0], false) };
                parts.Add(((SelectNode)Children[Children.Count - 1], Rule.Index == (int)RuleConstants.RULE_QUERY_UNION_ALL));

                return parts;
            }
        }

        internal override void CheckSyntax()
        {
            var parts = Parts;
            if (parts.Take(parts.Count - 1).Any(p => p.Select.OrderByClause != null))
            {
                throw new SqlException("Only the last part of a UNION can have an ORDER BY; it orders the whole UNION.", SqlErrorKind.Semantic);
            }

            if (parts.Select(p => p.Select.Columns is ColumnsStar ? -1 : p.Select.Columns.ColumnSources.Count()).Distinct().Count() > 1)
            {
                throw new SqlException("Each part of a UNION must select the same number of columns.", SqlErrorKind.Semantic);
            }
        }

        internal Expression Combine<TResult>(IList<Expression> results)
        {
            var parts = Parts;
            var star = parts[0].Select.Columns is ColumnsStar;
            var names = Names<TResult>(parts, star);
            var single = !star && names.Count == 1;
            var comparer = Expression.Constant(new UnionComparer<TResult>(row => Values(row, names, single)), typeof(IEqualityComparer<TResult>));

            var combined = results[0];
            for (var i = 1; i < parts.Count; i++)
            {
                combined = parts[i].All
                    ? Expression.Call(typeof(Enumerable), nameof(Enumerable.Concat), new[] { typeof(TResult) }, combined, results[i])
                    : Expression.Call(typeof(Enumerable), nameof(Enumerable.Union), new[] { typeof(TResult) }, combined, results[i], comparer);
            }

            var orderBy = parts[parts.Count - 1].Select.OrderByClause;

            return orderBy == null ? combined : ResultOrder.Order(combined, typeof(TResult), orderBy.OrderByItems.Select(i => (Key(i, names, single), i.IsDescending)).ToList());
        }

        private static List<string> Names<TResult>(List<(SelectNode Select, bool All)> parts, bool star)
        {
            if (star)
            {
                return typeof(TResult) == typeof(object) || typeof(TResult).IsDictionary() ? null : typeof(TResult).ColumnNames().ToList();
            }

            var names = parts[0].Select.Columns.ColumnSources.Select(c => c.ColumnAlias).ToList();
            var byName = typeof(TResult).IsDictionary() || names.Count > 1 && !typeof(TResult).IsTuple();
            if (byName && parts.Any(p => !p.Select.Columns.ColumnSources.Select(c => c.ColumnAlias).SequenceEqual(names, StringComparer.OrdinalIgnoreCase)))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "Each part of a UNION must name its columns like the first part ({0}); use AS to rename them.", string.Join(", ", names)), SqlErrorKind.Semantic);
            }

            return names;
        }

        private static object[] Values(object row, List<string> names, bool single)
        {
            if (names == null)
            {
                return ((IDictionary<string, object>)row)
                    .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .SelectMany(p => new[] { p.Key, SourcesExtensions.KeyOf(p.Value) })
                    .ToArray();
            }

            return names.Select((name, index) => SourcesExtensions.KeyOf(ResultOrder.Column(row, name, index, single))).ToArray();
        }

        private static Func<Expression, Expression> Key(OrderByItem item, List<string> names, bool single)
        {
            if (item is OrderByValueItem)
            {
                return row => row;
            }

            if (names == null)
            {
                if (item is OrderByPositionItem unordered)
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY {0} needs a select list; SELECT * over dictionary or dynamic rows has no fixed column order.", unordered.Position), SqlErrorKind.Semantic);
                }

                return row => ResultOrder.Column(row, item.Id.LocalId, -1, false);
            }

            var index = item is OrderByPositionItem position
                ? position.Column(Enumerable.Range(0, names.Count).ToList())
                : names.FindIndex(n => string.Equals(n, item.Id.LocalId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY {0} isn't a column of the UNION.", item.Id.LookupId), SqlErrorKind.UnknownName);
            }

            return row => ResultOrder.Column(row, names[index], index, single);
        }
    }
}
