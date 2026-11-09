using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_MIN_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_MIN_LPAREN_DISTINCT_RPAREN)]
    public class MinNode : AggregateNode
    {
        public MinNode()
        {
            Name = "Min";
        }

        internal override bool NullWhenEmpty
        {
            get
            {
                return true;
            }
        }

        internal override MethodCallExpression Ordered(Expression values, Type type)
        {
            return Pick(values, type, nameof(Enumerable.OrderBy), nameof(Enumerable.Min));
        }

        internal override object AggregateValues(IEnumerable<object> values)
        {
            return Extreme(values, -1);
        }
    }
}
