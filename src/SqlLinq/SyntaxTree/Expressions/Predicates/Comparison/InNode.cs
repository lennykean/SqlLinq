using System;
using System.Linq;
using System.Diagnostics;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Expressions.Literals;

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
            var widest = listType.Widest(left.Type)?.Lifted(listType);
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

            Expression body = Expression.Call(typeof(Enumerable), "Contains", new Type[] { left.Type }, list, left);
            var entries = Children.OfType<InListNode>().Single().FindChild<ExpressionNode>(1);
            var nulls = entries is NullLiteral || entries is ValueListNode all && all.Values().Any(v => v is NullLiteral);
            if (nulls || (list is NewArrayExpression literal ? literal.Expressions.Any(PredicateExpressionNode.MayBeNull) : !listType.IsValueType || Nullable.GetUnderlyingType(listType) != null))
            {
                var unknown = nulls
                    ? (Expression)Expression.Constant(null, typeof(bool?))
                    : Expression.Condition(Expression.Call(typeof(Enumerable), "Contains", new Type[] { listType }, list, Expression.Constant(null, listType)), Expression.Constant(null, typeof(bool?)), Expression.Constant(false, typeof(bool?)));
                body = Expression.OrElse(Expression.Convert(body, typeof(bool?)), unknown);
            }

            return TupleSelectNode.Guard(PredicateExpressionNode.Known(body, left));
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var list = CreateChildExpression(sourceData, param, 2);
            var left = CreateChildExpression(sourceData, param, 0);

            return Compiled(() => CreateInExpression(list, left));
        }
    }
}
