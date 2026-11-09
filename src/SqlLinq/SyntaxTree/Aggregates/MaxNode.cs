using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_MAX_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_MAX_LPAREN_DISTINCT_RPAREN)]
    public class MaxNode : AggregateNode
    {
        public MaxNode()
        {
            Name = "Max";
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
            return Pick(values, type, nameof(Enumerable.OrderByDescending), nameof(Enumerable.Max));
        }

        internal override object AggregateValues(IEnumerable<object> values)
        {
            return Extreme(values, 1);
        }
    }
}
