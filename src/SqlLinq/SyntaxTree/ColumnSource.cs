using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.SyntaxTree
{
    [SyntaxNode(RuleConstants.RULE_COLUMNSOURCE_ID2)]  // AS ColumnAlias
    [SyntaxNode(RuleConstants.RULE_COLUMNSOURCE_ID)]
    [SyntaxNode(RuleConstants.RULE_COLUMNSOURCE2)]
    public class ColumnSource : NonTerminalNode
    {
        private Identifier m_id;

        public ColumnSource()
        {
        }

        public virtual string ColumnAlias
        {
            get
            {
                NodeWithId alias = FindChild<NodeWithId>(RuleConstants.RULE_COLUMNALIAS_AS_ID);
                if (alias != null)
                    return alias.Id.LocalId;

                return Function == null ? Id.LocalId : Function.Name;
            }
        }

        public ISqlFunction Function { get; internal set; }

        public string FunctionName { get; internal set; }

        public IReadOnlyList<ArgumentNode> Arguments { get; internal set; }

        internal IEnumerable<NodeWithId> ArgumentFields
        {
            get
            {
                return NodeWithId.ArgumentFields(Arguments ?? Enumerable.Empty<ArgumentNode>());
            }
        }

        public override string ToSql()
        {
            return Function == null ? base.ToSql() : JoinSql(Children.Select(c => c is TerminalNode ? NodeWithId.Call(FunctionName, Arguments) : c.ToSql()));
        }

        internal override void CopyTo(NonTerminalNode copy, Func<NonTerminalNode, NonTerminalNode> replacement)
        {
            var column = (ColumnSource)copy;
            column.Function = Function;
            column.FunctionName = FunctionName;
            column.Arguments = Arguments?.Select(a => a.Copy(column, replacement)).ToList();
        }

        internal virtual Expression CreateRowValue(ParameterExpression rows, ParameterExpression row)
        {
            if (Function != null)
            {
                return Function.Compile(Arguments.Select(a => new SqlArgument(a.Text, a.Expression.CreateExpression(rows, row))).ToList());
            }

            return SelectNode.CreateFieldSelector(EnclosingSelect(), Id, row);
        }

        protected virtual Identifier CreateIdentifier()
        {
            return SqlSyntax.Name(GetTerminal("Id"));
        }

        public Identifier Id
        {
            get
            {
                if (m_id == null)
                    m_id = CreateIdentifier();

                return m_id;
            }
        }

        internal virtual IEnumerable<Identifier> GetFields()
        {
            yield return this.Id;
        }
    }
}

