using System;
using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_COUNT_LPAREN_TIMES_RPAREN)]
    public class CountStarNode : CountNode
    {
        public CountStarNode()
        {
            Name = "Count";
        }

        protected override Identifier CreateIdentifier()
        {
            return new Identifier(Name + "Star");
        }

        public override bool DoNotDereferenceFields
        {
            get
            {
                return true;
            }
        }

        internal override Expression GetCallExpression(Type tSource, Expression param)
        {
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), new[] { tSource }, param);
        }

        protected override Type EvaluatatorType
        {
            get
            {
                return typeof(Enumerable);
            }
        }
    }
}
