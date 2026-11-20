using System;
using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Globalization;

using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_GROUPCLAUSE_GROUP_BY)]
    public class GroupByClause : NonTerminalNode
    {
        public GroupByClause()
        {
        }

        public IEnumerable<NodeWithId> GroupByItems
        {
            get
            {
                return FindDescendants<NodeWithId>();
            }
        }

        public Func<IEnumerable<TSource>, IEnumerable<TResult>> CreateGroupBySelector<TSource, TResult>(IEnumerable<AggregateNode> aggregates)
        {
            var list = Expression.Parameter(typeof(IEnumerable<TSource>), "list");

            return Expression.Lambda<Func<IEnumerable<TSource>, IEnumerable<TResult>>>(CreateGroups(list, typeof(TSource), typeof(TResult), null), list).Compile();
        }

        internal Expression CreateGroups(ParameterExpression rows, Type tSource, Type tResult, HavingClause having)
        {
            var select = EnclosingSelect();
            var item = Expression.Parameter(tSource, "item");
            var keys = GroupByItems.Select(g => g.CreateExpression(rows, item)).ToList();
            Validate();

            var tuple = NewKey(keys);
            var keyType = tuple.Type;
            var sequence = QueryTree.Sequence(tSource);
            var key = Expression.Parameter(keyType, "key");
            var group = Expression.Parameter(sequence, "group");
            Expression groups = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.GroupBy),
                new[] { tSource, keyType, sequence },
                rows,
                Expression.Lambda(tuple, item),
                Expression.Lambda(group, key, group));

            if (having != null)
            {
                groups = QueryTree.Where(groups, sequence, g => having.CreateGroupPredicate(g, tSource));
            }

            return QueryTree.Select(groups, sequence, tResult, g => select.Columns.CreateGroupResult(g, tSource, tResult));
        }

        internal void Validate()
        {
            var select = EnclosingSelect();
            var keys = new HashSet<string>(GroupFields(), StringComparer.OrdinalIgnoreCase);
            var columns = select.Columns.ColumnSources.ToList();
            var selected = columns.Where(c => !(c is AggregateNode) && !(c is ComputedColumn) && c.Function == null).Select(c => c.Id);
            var called = columns.Where(c => c.Function != null && !keys.Contains(NodeWithId.Call(c.FunctionName, c.Arguments)));
            bool grouped(NodeWithId f) => f.AncestorsBelow(select).OfType<NodeWithId>().Any(c => c.Function != null && keys.Contains(c.ToSql()));
            var computed = columns.OfType<ComputedColumn>().SelectMany(c => c.Fields).Concat(called.SelectMany(c => c.ArgumentFields)).Where(f => !grouped(f)).Select(f => f.Id);

            var ungrouped = selected.Concat(computed).FirstOrDefault(f => !keys.Contains(select.Field(f.LookupId)));
            if (ungrouped != null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, "The field '{0}' can't be selected because it isn't in the GROUP BY clause.", ungrouped.LookupId);
                var unknown = GroupByItems.FirstOrDefault(g => !select.Owns(g.Id));
                if (unknown != null)
                {
                    message += string.Format(CultureInfo.CurrentCulture, " {1} can't be read because {0} isn't a source or alias of this query.", unknown.Id.Qualifier, unknown.Id.LookupId);
                }

                throw new SqlException(message, SqlErrorKind.Semantic, ungrouped.Start, ungrouped.Length);
            }

            var having = select.HavingClause;
            var aliases = columns.Where(c => c is AggregateNode || c is ComputedColumn || !string.Equals(c.Id.LocalId, c.ColumnAlias, StringComparison.OrdinalIgnoreCase)).Select(c => c.ColumnAlias).ToList();
            var filtered = having?.FindDescendants<NodeWithId>()
                .SelectMany(n => n.Fields)
                .Where(n => n.EnclosingSelect() == select && select.Owns(n.Id) && !grouped(n) && !n.AncestorsBelow(having).OfType<AggregateNode>().Any())
                .Select(n => n.Id)
                .FirstOrDefault(id => !(id.Qualifier.Length == 0 && aliases.Contains(id.LookupId, StringComparer.OrdinalIgnoreCase)) && !keys.Contains(select.Field(id.LookupId)));
            if (filtered != null)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "The field '{0}' can't be used in HAVING because it isn't in the GROUP BY clause.", filtered.LookupId), SqlErrorKind.Semantic, filtered.Start, filtered.Length);
            }
        }

        private List<string> GroupFields()
        {
            var select = EnclosingSelect();

            return GroupByItems.Select(g => g.Function == null ? select.Field(g.Id.LookupId) : g.ToSql()).ToList();
        }

        private static NewExpression NewKey(List<Expression> keys)
        {
            var items = keys.Take(7).ToList();
            if (keys.Count > 7)
            {
                items.Add(NewKey(keys.Skip(7).ToList()));
            }

            var types = items.Select(k => k.Type).ToArray();

            return Expression.New(GetTupleTemplate(types.Length).MakeGenericType(types).GetConstructor(types), items);
        }

        private static Type GetTupleTemplate(int typeArgumentCount)
        {
            Debug.Assert(typeArgumentCount >= 1 && typeArgumentCount <= 8);
            switch (typeArgumentCount)
            {
                case 1: return typeof(Tuple<>);
                case 2: return typeof(Tuple<,>);
                case 3: return typeof(Tuple<,,>);
                case 4: return typeof(Tuple<,,,>);
                case 5: return typeof(Tuple<,,,,>);
                case 6: return typeof(Tuple<,,,,,>);
                case 7: return typeof(Tuple<,,,,,,>);
                case 8: return typeof(Tuple<,,,,,,,>);
            }

            throw new ArgumentException("A group key can't have more than 8 fields.", nameof(typeArgumentCount));
        }
    }
}