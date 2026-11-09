using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using LinqStatistics;

namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_VAR_LPAREN_RPAREN)]
    public class VarianceNode : AggregateNode
    {
        public VarianceNode()
        {
            Name = "Variance";
        }

        internal override bool NullWhenEmpty
        {
            get
            {
                return true;
            }
        }

        internal virtual bool Sample
        {
            get
            {
                return true;
            }
        }

        internal override Expression Complete(Expression values, MethodCallExpression aggregate)
        {
            var type = values.Type.GetGenericArguments()[0];
            if (!Sample || Nullable.GetUnderlyingType(type) == null)
            {
                return aggregate;
            }

            var present = Known(values, known => Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), new[] { type }, values, known));

            return Expression.Condition(Expression.GreaterThan(present, Expression.Constant(1)), aggregate, ExpressionFactory.Nothing(aggregate.Type));
        }

        internal override object AggregateValues(IEnumerable<object> values)
        {
            var present = values.Where(v => v != null).ToList();

            return Sample && present.Count < 2 ? null : base.AggregateValues(present);
        }

        protected override Type EvaluatatorType
        {
            get
            {
                return typeof(EnumerableStats);
            }
        }
    }
}
