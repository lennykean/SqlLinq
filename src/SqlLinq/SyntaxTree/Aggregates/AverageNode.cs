using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

using SqlLinq.SyntaxTree.Expressions.Arithmetic;

namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_AVG_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_AVG_LPAREN_DISTINCT_RPAREN)]
    public class AverageNode : AggregateNode
    {
        public AverageNode()
        {
            Name = "Average";
        }

        internal override bool NullWhenEmpty
        {
            get
            {
                return true;
            }
        }

        internal override object AggregateValues(IEnumerable<object> values)
        {
            var present = values.Where(v => v != null).ToList();
            if (present.Count == 0)
            {
                return null;
            }

            return present.All(ArithmeticExpressionNode.IsWhole)
                ? present.Average(v => Convert.ToInt64(v, CultureInfo.InvariantCulture))
                : present.Average(v => ArithmeticExpressionNode.ToNumber(v).Value);
        }
    }
}
