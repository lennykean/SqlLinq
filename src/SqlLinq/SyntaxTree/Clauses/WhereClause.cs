using System;
using System.Linq;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Collections.Generic;

using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_WHERECLAUSE_WHERE)]
    public class WhereClause : NonTerminalNode
    {
        public WhereClause()
        {
        }

        public Func<IEnumerable<T>, T, bool> CreateEvaluator<T>()
        {
            ExpressionNode predicate = FindChild<ExpressionNode>();
            if (predicate != null)
            {
                var sourceData = Expression.Parameter(typeof(IEnumerable<T>));
                return predicate.CreatePredicateFunction<T>(sourceData);
            }
            // shouldn't get here but...
            // any other expression in the where clause evaluates to true
            Debug.Assert(false);
            return (e, t) => true;
        }

        internal Expression CreatePredicate(ParameterExpression sourceData, ParameterExpression row)
        {
            return FindChild<ExpressionNode>().CreatePredicate(sourceData, row);
        }

        internal override void CheckSyntax()
        {
            // filter out any aggregates expressions in a where clause that are not part of a sub-query
            if (FindDescendants<AggregateNode>().Any(a => !a.AncestorsBelow(this).OfType<SelectNode>().Any()))
                throw new SqlException("WHERE can't use an aggregate because it filters rows before grouping.", SqlErrorKind.Semantic);
        }
    }
}
