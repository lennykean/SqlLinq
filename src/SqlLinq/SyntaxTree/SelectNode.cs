using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;

using SqlLinq.SyntaxTree.Clauses;
using SqlLinq.SyntaxTree.Joins;

namespace SqlLinq.SyntaxTree
{
    [SyntaxNode(RuleConstants.RULE_SELECTSTM_SELECT)]
    public class SelectNode : NonTerminalNode
    {
        private HashSet<string> _names;

        private bool? _joined;

        public SelectNode()
        {
        }

        internal HashSet<string> Names
        {
            get
            {
                if (_names == null)
                {
                    var joined = FromClause.FindDescendants<Join>().SelectMany(j => FromClause.Of(j.Children.OfType<NonTerminalNode>().First()));
                    _names = new HashSet<string>(FromClause.Members.Concat(joined).Select(m => m.Alias ?? m.Name), StringComparer.OrdinalIgnoreCase);
                }

                return _names;
            }
        }

        internal bool Joined
        {
            get
            {
                return _joined ?? FromClause.Join != null;
            }
            set
            {
                _joined = value;
            }
        }

        internal IEnumerable Source { get; set; }

        internal JoinLayout Layout { get; set; }

        internal Expression OuterRow { get; set; }

        internal List<(ParameterExpression Parameter, List<(ParameterExpression Value, Expression Body)> Materialized)> Scopes { get; } = new List<(ParameterExpression Parameter, List<(ParameterExpression Value, Expression Body)> Materialized)>();

        internal bool Owns(Identifier id)
        {
            return id.Qualifier.Length == 0 || Names.Contains(id.Qualifier);
        }

        internal string Field(string lookupId)
        {
            var id = new Identifier(lookupId);

            return Layout != null ? Layout.Field(id) : !Joined && Names.Contains(id.Qualifier) ? id.LocalId : lookupId;
        }

        internal static Expression CreateFieldSelector(SelectNode scope, Identifier id, ParameterExpression row)
        {
            Expression source = row;
            for (var level = scope; source != null; level = level.EnclosingSelect())
            {
                if (level == null)
                {
                    if (id.Qualifier.Length == 0 || string.Equals(id.Qualifier, "this", StringComparison.OrdinalIgnoreCase))
                    {
                        return ExpressionFactory.CreateFieldSelector(source, id.Qualifier.Length == 0 ? id.LookupId : id.LocalId, id);
                    }

                    break;
                }

                if (level.Owns(id))
                {
                    return level.Layout == null ? ExpressionFactory.CreateFieldSelector(source, level.Field(id.LookupId), id) : level.Layout.Read(source, id);
                }

                source = level.OuterRow;
            }

            if (scope?.Layout != null)
            {
                return scope.Layout.Read(row, id);
            }

            if (row.Type == typeof(object) || row.Type.IsDictionary())
            {
                var valueType = row.Type == typeof(object) ? typeof(object) : row.Type.GetDictionaryValueType();

                return Expression.Call(typeof(SelectNode), nameof(DottedKey), new[] { valueType }, Expression.Convert(row, typeof(object)), Expression.Constant(id));
            }

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{1} can't be read because {0} isn't a source or alias of this query.", id.Qualifier, id.LookupId), SqlErrorKind.UnknownName, id.Start, id.Length);
        }

        private static TValue DottedKey<TValue>(object row, Identifier id)
        {
            if (row is IDictionary<string, TValue> values)
            {
                if (values.TryGetValue(id.LookupId, out var value))
                {
                    return value;
                }

                var match = row is ExpandoObject ? values.FirstOrDefault(v => string.Equals(v.Key, id.LookupId, StringComparison.OrdinalIgnoreCase)) : default;
                if (match.Key != null)
                {
                    return match.Value;
                }
            }

            throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{1} can't be read because {0} isn't a source or alias of this query, or a field of the row.", id.Qualifier, id.LookupId), SqlErrorKind.UnknownName, id.Start, id.Length);
        }

        public Columns Columns
        {
            get
            {
                // SELECT <field list> and SELECT * have slightly different structures
                return FindChild<Columns>(RuleConstants.RULE_COLUMNS) ?? FindChild<Columns>(RuleConstants.RULE_COLUMNS_TIMES);
            }
        }

        public FromClause FromClause
        {
            get
            {
                return FindChild<FromClause>();
            }
        }

        public WhereClause WhereClause
        {
            get
            {
                return FindChild<WhereClause>(RuleConstants.RULE_WHERECLAUSE_WHERE);
            }
        }

        public OrderByClause OrderByClause
        {
            get
            {
                return FindChild<OrderByClause>();
            }
        }

        public GroupByClause GroupByClause
        {
            get
            {
                return FindChild<GroupByClause>();
            }
        }

        public HavingClause HavingClause
        {
            get
            {
                return FindChild<HavingClause>(RuleConstants.RULE_HAVINGCLAUSE_HAVING);
            }
        }

        internal override void CheckSyntax()
        {
            if (GroupByClause == null && Columns.Aggregates.Any() && !Columns.IsAggregateOnlyQuery)
                throw new SqlException("Fields and aggregates can't be selected together without GROUP BY.\n", SqlErrorKind.Semantic);

            if ((Columns.Distinct || GroupByClause != null) && OrderByClause != null)
            {
                var selectFields = Columns.FieldList;
                foreach (OrderByItem item in OrderByClause.OrderByItems)
                {
                    if (item.SelectColumn() == null && !selectFields.Contains(item.Id.LookupId, StringComparer.OrdinalIgnoreCase) && !item.Id.LookupId.StartsWith(SqlParser.Placeholder, StringComparison.Ordinal))
                        throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY '{0}' can't be used with DISTINCT or GROUP BY because it isn't in the select list.", item.Id.LookupId), SqlErrorKind.Semantic);
                }
            }

            base.CheckSyntax();
        }
    }
}
