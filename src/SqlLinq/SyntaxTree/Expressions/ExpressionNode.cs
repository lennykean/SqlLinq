using System;
using System.Collections.Generic;
using System.Globalization;
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

        internal string Source { get; set; }

        public Func<IEnumerable<T>, T, bool> CreatePredicateFunction<T>(ParameterExpression sourceData)
        {
            var param = Expression.Parameter(typeof(T), "arg");

            return Expression.Lambda<Func<IEnumerable<T>, T, bool>>(CreatePredicate(sourceData, param), sourceData, param).Compile();
        }

        internal Expression CreatePredicate(ParameterExpression sourceData, ParameterExpression param)
        {
            var expression = CreateExpression(sourceData, param);
            if (expression.Type == typeof(bool?))
            {
                expression = Expression.Equal(expression, Expression.Constant(true, typeof(bool?)));
            }
            else if (Nullable.GetUnderlyingType(expression.Type) is Type underlying)
            {
                expression = Expression.Coalesce(expression, Expression.Default(underlying));
            }

            // if the expression doesn't return a boolean add a clause to convert the result to bool
            if (expression.Type != typeof(bool))
            {
                MethodInfo convert = typeof(Convert).GetMethod("ToBoolean", new Type[] { expression.Type });
                if (convert == null || expression.Type.IsValueType && convert.GetParameters()[0].ParameterType != expression.Type)
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be used as a condition because it's a {1}.", Source ?? ToSql(), expression.Type.Name), SqlErrorKind.Semantic, Start, Length);
                }

                expression = Expression.Convert(expression, typeof(bool), convert);
            }

            return expression;
        }

        internal Expression Compiled(Func<Expression> create)
        {
            try
            {
                return create();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be compiled because its types don't go together: {1}", Source ?? ToSql(), e.Message), e, SqlErrorKind.Semantic, Start, Length);
            }
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
