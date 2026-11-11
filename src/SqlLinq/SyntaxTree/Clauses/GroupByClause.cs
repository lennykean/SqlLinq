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

        internal override void CheckSyntax()
        {
            if (GroupByItems.Count() > 8)
                throw new SqlException("Only 8 fields or less are currently supported for grouping");

            base.CheckSyntax();
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
            var keys = GroupByItems.Select(g => SelectNode.CreateFieldSelector(select, g.Id, item)).ToList();
            Validate();

            var types = keys.Select(k => k.Type).ToArray();
            var keyType = GetTupleTemplate(types.Length).MakeGenericType(types);
            var sequence = QueryTree.Sequence(tSource);
            var key = Expression.Parameter(keyType, "key");
            var group = Expression.Parameter(sequence, "group");
            Expression groups = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.GroupBy),
                new[] { tSource, keyType, sequence },
                rows,
                Expression.Lambda(Expression.New(keyType.GetConstructor(types), keys), item),
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
            var selected = columns.Where(c => !(c is AggregateNode) && !(c is ComputedColumn) && c.Function == null).Select(c => c.Id.LookupId);
            var computed = columns.OfType<ComputedColumn>().SelectMany(c => c.Fields).Concat(columns.SelectMany(c => c.ArgumentFields)).Select(f => f.Id.LookupId);

            var ungrouped = selected.Concat(computed).FirstOrDefault(f => !keys.Contains(select.Field(f)));
            if (ungrouped != null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, "The query contains the field '{0}' that is not matched between the select list and group by clause.", ungrouped);
                var unknown = GroupByItems.FirstOrDefault(g => !select.Owns(g.Id));
                if (unknown != null)
                {
                    message += string.Format(CultureInfo.CurrentCulture, " {0} in {1} isn't a source or alias of this query.", unknown.Id.Qualifier, unknown.Id.LookupId);
                }

                throw new SqlException(message);
            }
        }

        private List<string> GroupFields()
        {
            var select = EnclosingSelect();

            return GroupByItems.Select(g => select.Field(g.Id.LookupId)).ToList();
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

            throw new ArgumentException("Expressions greater than 8 fields are not supported.", nameof(typeArgumentCount));
        }
    }
}