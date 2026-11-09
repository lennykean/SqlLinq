using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Arithmetic;

namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_SUM_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_SUM_LPAREN_DISTINCT_RPAREN)]
    public class SumNode : AggregateNode
    {
        public SumNode()
        {
            Name = "Sum";
        }

        internal override bool NullWhenEmpty
        {
            get
            {
                return true;
            }
        }

        internal override Expression Complete(Expression values, MethodCallExpression aggregate)
        {
            if (Nullable.GetUnderlyingType(aggregate.Type) == null)
            {
                return aggregate;
            }

            var type = values.Type.GetGenericArguments()[0];
            var present = Known(values, known => Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { type }, values, known));

            return Expression.Condition(present, aggregate, ExpressionFactory.Nothing(aggregate.Type));
        }

        internal override object AggregateValues(IEnumerable<object> values)
        {
            var present = values.Where(v => v != null).ToList();
            if (present.Count == 0)
            {
                return null;
            }

            if (!present.All(ArithmeticExpressionNode.IsWhole))
            {
                return present.Sum(v => ArithmeticExpressionNode.ToNumber(v).Value);
            }

            var total = present.Aggregate(0L, (sum, value) => checked(sum + Convert.ToInt64(value, CultureInfo.InvariantCulture)));

            return Convert.ChangeType(total, present.All(ArithmeticExpressionNode.IsSmall) ? typeof(int) : typeof(long), CultureInfo.InvariantCulture);
        }
    }
}
