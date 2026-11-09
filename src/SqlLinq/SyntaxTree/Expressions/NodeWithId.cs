using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Expressions
{
    [SyntaxNode(RuleConstants.RULE_IDMEMBER_ID)]
    [SyntaxNode(RuleConstants.RULE_VALUE_ID)]
    [SyntaxNode(RuleConstants.RULE_COLUMNALIAS_AS_ID)]
    public class NodeWithId : ExpressionNode
    {
        private Lazy<Identifier> _id;

        public NodeWithId()
        {
            _id = new Lazy<Identifier>(() => SqlSyntax.Name(GetTerminalText("Id")));
        }

        public Identifier Id
        {
            get
            {
                return _id.Value;
            }
        }

        internal ISqlFunction Function { get; set; }

        internal IReadOnlyList<(string Text, ExpressionNode Node)> Arguments { get; set; }

        internal IEnumerable<NodeWithId> Fields
        {
            get
            {
                return Function == null ? new[] { this } : ArgumentFields(Arguments);
            }
        }

        internal static IEnumerable<NodeWithId> ArgumentFields(IEnumerable<(string Text, ExpressionNode Node)> arguments)
        {
            return arguments
                .SelectMany(a => new[] { a.Node }.Concat(a.Node.FindDescendants<ExpressionNode>()))
                .OfType<NodeWithId>()
                .SelectMany(n => n.Fields);
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            if (Function != null)
            {
                return Function.Compile(Arguments.Select(a => new SqlArgument(a.Text, a.Node.CreateExpression(sourceData, param))).ToList());
            }

            var alias = HavingAlias();
            if (alias is AggregateNode aggregate)
            {
                return aggregate.GetCallExpression(sourceData.Type.GetGenericArguments()[0], sourceData);
            }

            if (alias != null)
            {
                return alias.CreateRowValue(sourceData, param);
            }

            return EnclosingSelect().CreateFieldSelector(Id, param);
        }

        private ColumnSource HavingAlias()
        {
            var select = EnclosingSelect();
            var having = select?.HavingClause;
            if (having == null || !having.CompilingOverGroups || Id.Qualifier.Length > 0 || !AncestorsBelow(select).Contains(having))
            {
                return null;
            }

            return select.Columns.ColumnSources.FirstOrDefault(c =>
                string.Equals(c.ColumnAlias, Id.LookupId, StringComparison.OrdinalIgnoreCase)
                && (c is AggregateNode || c is ComputedColumn || !string.Equals(c.Id.LocalId, Id.LookupId, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
