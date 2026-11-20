using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_NOT_IN)]
    public class NotInNode : InNode
    {
        public NotInNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var list = CreateChildExpression(sourceData, param, 3);
            var left = CreateChildExpression(sourceData, param, 0);

            return Expression.Not(Compiled(() => CreateInExpression(list, left)));
        }
    }
}
