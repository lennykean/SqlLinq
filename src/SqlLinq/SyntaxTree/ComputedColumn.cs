using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Aggregates;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.SyntaxTree
{
    [SyntaxNode(RuleConstants.RULE_COLUMNSOURCE3)]
    internal sealed class ComputedColumn : ColumnSource
    {
        internal bool HasAggregates
        {
            get
            {
                return Own<AggregateNode>().Any();
            }
        }

        internal IEnumerable<NodeWithId> Fields
        {
            get
            {
                return Own<NodeWithId>().SelectMany(n => n.Fields);
            }
        }

        internal override Expression CreateRowValue(ParameterExpression rows, ParameterExpression row)
        {
            return Body.CreateExpression(rows, row);
        }

        internal override IEnumerable<Identifier> GetFields()
        {
            return Enumerable.Empty<Identifier>();
        }

        private ExpressionNode Body
        {
            get
            {
                return (ExpressionNode)Children[0];
            }
        }

        private IEnumerable<T> Own<T>() where T : NonTerminalNode
        {
            return Body.FindDescendants<T>().Where(n => !n.AncestorsBelow(this).Any(a => a is AggregateNode || a is SelectNode));
        }
    }
}
