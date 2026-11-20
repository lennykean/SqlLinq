using System;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_BETWEEN_AND)]
    public class BetweenNode : ExpressionNode
    {
        public BetweenNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            return CreateBetweenExpression(sourceData, param, CreateChildExpression(sourceData, param, 2), CreateChildExpression(sourceData, param, 4));
        }

        protected Expression CreateBetweenExpression(ParameterExpression sourceData, ParameterExpression param, Expression start, Expression end)
        {
            var expression = CreateChildExpression(sourceData, param, 0);

            return Compiled(() =>
            {
                Type widest = expression.Type.Widest(start.Type, end.Type);
                widest = widest?.Lifted(expression.Type, start.Type, end.Type);

                if (expression.Type != widest)
                    expression = ExpressionFactory.ConvertTo(expression, widest);

                if (start.Type != widest)
                    start = ExpressionFactory.ConvertTo(start, widest);

                if (end.Type != widest)
                    end = ExpressionFactory.ConvertTo(end, widest);

                var gte = PredicateExpressionNode.Compare(expression, start, Expression.GreaterThanOrEqual);
                var lte = PredicateExpressionNode.Compare(expression, end, Expression.LessThanOrEqual);
                if (gte.Type != lte.Type)
                {
                    gte = Expression.Convert(gte, typeof(bool?));
                    lte = Expression.Convert(lte, typeof(bool?));
                }

                return TupleSelectNode.Guard(Expression.AndAlso(gte, lte));
            });
        }
    }
}
