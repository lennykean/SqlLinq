using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Reflection;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree.Expressions
{
    public abstract class ExpressionNode : NonTerminalNode
    {
        protected ExpressionNode()
        {
        }

        public Func<IEnumerable<T>, T, bool> CreatePredicateFunction<T>(ParameterExpression sourceData)
        {
            var param = Expression.Parameter(typeof(T), "arg");

            return Expression.Lambda<Func<IEnumerable<T>, T, bool>>(CreatePredicate(sourceData, param), sourceData, param).Compile();
        }

        internal Expression CreatePredicate(ParameterExpression sourceData, ParameterExpression param)
        {
            var expression = CreateExpression(sourceData, param);

            // if the expression doesn't return a boolean add a clause to convert the result to bool
            if (expression.Type != typeof(bool))
            {
                MethodInfo convert = typeof(Convert).GetMethod("ToBoolean", new Type[] { expression.Type });
                Debug.Assert(convert != null);

                expression = Expression.Convert(expression, typeof(bool), convert);
            }

            return expression;
        }

        internal abstract Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param);

        protected Expression CreateChildExpression(ParameterExpression sourceData, ParameterExpression param, int index)
        {
            if (FindChild<AggregateNode>(index) is AggregateNode aggregate)
            {
                return aggregate.GetCallExpression(sourceData.Type.GetGenericArguments()[0], sourceData);
            }

            ExpressionNode expression = FindChild<ExpressionNode>(index);
            Debug.Assert(expression != null);
            return expression.CreateExpression(sourceData, param);
        }
    }
}
