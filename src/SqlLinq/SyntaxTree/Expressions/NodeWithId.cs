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
            _id = new Lazy<Identifier>(() => SqlSyntax.Name(GetTerminal("Id")));
        }

        public Identifier Id
        {
            get
            {
                return _id.Value;
            }
        }

        public ISqlFunction Function { get; internal set; }

        public string FunctionName { get; internal set; }

        public IReadOnlyList<ArgumentNode> Arguments { get; internal set; }

        internal IEnumerable<NodeWithId> Fields
        {
            get
            {
                return Function == null ? new[] { this } : ArgumentFields(Arguments);
            }
        }

        internal static IEnumerable<NodeWithId> ArgumentFields(IEnumerable<ArgumentNode> arguments)
        {
            return arguments
                .Where(a => a.Expression != null)
                .SelectMany(a => new[] { a.Expression }.Concat(a.Expression.FindDescendants<ExpressionNode>()))
                .OfType<NodeWithId>()
                .SelectMany(n => n.Fields);
        }

        public override string ToSql()
        {
            return Function == null ? base.ToSql() : Call(FunctionName, Arguments);
        }

        internal static string Call(string name, IEnumerable<ArgumentNode> arguments)
        {
            return name + "(" + string.Join(", ", arguments.Select(a => a.ToSql())) + ")";
        }

        internal override void CopyTo(NonTerminalNode copy, Func<NonTerminalNode, NonTerminalNode> replacement)
        {
            var call = (NodeWithId)copy;
            call.Function = Function;
            call.FunctionName = FunctionName;
            call.Arguments = Arguments?.Select(a => a.Copy(call, replacement)).ToList();
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            if (Function != null)
            {
                return Function.Compile(Arguments.Select(a => new SqlArgument(a.Text, a.Expression.CreateExpression(sourceData, param))).ToList());
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

            return SelectNode.CreateFieldSelector(EnclosingSelect(), Id, param);
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
