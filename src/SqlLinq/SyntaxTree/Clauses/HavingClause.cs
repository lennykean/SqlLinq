using System;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_HAVINGCLAUSE_HAVING)]
    public class HavingClause : WhereClause
    {
        public HavingClause()
        {
        }

        internal bool HasAggregates
        {
            get
            {
                return FindDescendants<AggregateNode>().Any(a => a.EnclosingSelect() == Parent);
            }
        }

        internal bool CompilingOverGroups { get; private set; }

        internal Expression CreateGroupPredicate(ParameterExpression group, Type tSource)
        {
            CompilingOverGroups = true;

            try
            {
                var row = Expression.Parameter(tSource, "row");
                var first = Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { tSource }, group);

                return ParameterReplacer.Replace(CreatePredicate(group, row), row, first);
            }
            finally
            {
                CompilingOverGroups = false;
            }
        }

        internal override void CheckSyntax()
        {
        }
    }
}
