using System;
using System.Linq;
using System.Diagnostics;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_IN)]
    public class InNode : ExpressionNode
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        protected Expression CreateInExpression(Expression list, Expression left)
        {
            Debug.Assert(list.Type.IsArray);

            var listType = list.Type.GetElementType();
            var widest = listType.Widest(left.Type);
            if (widest != null && widest != listType && list is NewArrayExpression array)
            {
                list = Expression.NewArrayInit(widest, array.Expressions.Select(e => e.Type == widest ? e : ExpressionFactory.ConvertTo(e, widest)));
                listType = widest;
            }

            var lifted = listType.Lifted(left.Type);
            if (lifted != listType && list is NewArrayExpression values)
            {
                list = Expression.NewArrayInit(lifted, values.Expressions.Select(e => ExpressionFactory.ConvertTo(e, lifted)));
                listType = lifted;
            }

            if (left.Type != listType)
            {
                left = ExpressionFactory.ConvertTo(left, listType);
            }

            var body = Expression.Call(typeof(Enumerable), "Contains", new Type[] { left.Type }, list, left);
            return TupleSelectNode.Guard(body);
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            return CreateInExpression(CreateChildExpression(sourceData, param, 2), CreateChildExpression(sourceData, param, 0));
        }
    }
}
