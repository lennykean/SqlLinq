using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;

using SqlLinq.SyntaxTree.Expressions.Literals;

namespace SqlLinq.SyntaxTree.Expressions
{
    [SyntaxNode(RuleConstants.RULE_VALUELIST_COMMA)]
    public class ValueListNode : ExpressionNode
    {
        public ValueListNode()
        {

        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var values = Values().Where(v => !(v is NullLiteral)).Select(v => v.CreateExpression(sourceData, param)).ToList();
            if (values.Count == 0)
            {
                return Expression.NewArrayInit(typeof(object));
            }

            var type = values.Select(v => v.Type).Aggregate((widest, next) => widest.Widest(next) ?? typeof(object));

            return Expression.NewArrayInit(type, values.Select(v => v.Type == type ? v : ExpressionFactory.ConvertTo(v, type)));
        }

        private IEnumerable<ExpressionNode> Values()
        {
            foreach (var child in Children.OfType<ExpressionNode>())
            {
                if (child is ValueListNode list)
                {
                    foreach (var value in list.Values())
                    {
                        yield return value;
                    }
                }
                else
                {
                    yield return child;
                }
            }
        }
    }
}
