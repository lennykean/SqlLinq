using System;
using System.Collections.Generic;
using System.Globalization;
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
                return Function == null && !Id.LookupId.StartsWith(SqlParser.Placeholder, StringComparison.Ordinal) ? new[] { this } : ArgumentFields(Arguments ?? Enumerable.Empty<ArgumentNode>());
            }
        }

        internal static IEnumerable<NodeWithId> ArgumentFields(IEnumerable<ArgumentNode> arguments)
        {
            return arguments
                .Where(a => a.Expression != null)
                .SelectMany(a => new[] { a.Expression }.Concat(a.Expression.FindDescendants<ExpressionNode>().Where(e => !e.AncestorsBelow(a.Expression).Any(p => p is AggregateNode || p is SelectNode))))
                .OfType<NodeWithId>()
                .SelectMany(n => n.Fields);
        }

        internal IEnumerable<AggregateNode> Aggregates
        {
            get
            {
                return Function == null ? Enumerable.Empty<AggregateNode>() : ArgumentAggregates(Arguments);
            }
        }

        internal static IEnumerable<AggregateNode> ArgumentAggregates(IEnumerable<ArgumentNode> arguments)
        {
            return arguments
                .Where(a => a.Expression != null)
                .SelectMany(a => a.Expression.FindDescendants<AggregateNode>()
                    .Where(g => !g.AncestorsBelow(a.Expression).OfType<SelectNode>().Any())
                    .Concat(new[] { a.Expression }.Concat(a.Expression.FindDescendants<ExpressionNode>()).OfType<NodeWithId>().SelectMany(n => n.Aggregates)));
        }

        public override string ToSql()
        {
            return Function == null ? base.ToSql() : Call(FunctionName, Arguments);
        }

        internal static string Call(string name, IEnumerable<ArgumentNode> arguments)
        {
            return name + "(" + string.Join(", ", arguments.Select(a => a.ToSql())) + ")";
        }

        internal static Expression Compile(ISqlFunction function, string name, IEnumerable<ArgumentNode> arguments, ParameterExpression rows, ParameterExpression row, SyntaxNode call)
        {
            Expression compiled;
            try
            {
                compiled = function.Compile(arguments.Select(a => new SqlArgument(a.Text, a.Expression.CreateExpression(rows, row))).ToList());
            }
            catch (SqlException e) when (e.Start < 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, call.Start, call.Length);
            }

            return compiled ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The function {0} can't be called because its Compile returned no expression.", name));
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
                return Compile(Function, FunctionName, Arguments, sourceData, param, this);
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
