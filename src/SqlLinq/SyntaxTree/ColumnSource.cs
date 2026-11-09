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

        internal ISqlFunction Function { get; set; }

        internal IReadOnlyList<(string Text, ExpressionNode Node)> Arguments { get; set; }

        internal IEnumerable<NodeWithId> ArgumentFields
        {
            get
            {
                return NodeWithId.ArgumentFields(Arguments ?? Enumerable.Empty<(string Text, ExpressionNode Node)>());
            }
        }

        internal virtual Expression CreateRowValue(ParameterExpression rows, ParameterExpression row)
        {
            if (Function != null)
            {
                return Function.Compile(Arguments.Select(a => new SqlArgument(a.Text, a.Node.CreateExpression(rows, row))).ToList());
            }

            return EnclosingSelect().CreateFieldSelector(Id, row);
        }

        protected virtual Identifier CreateIdentifier()
        {
            return SqlSyntax.Name(GetTerminalText("Id"));
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

