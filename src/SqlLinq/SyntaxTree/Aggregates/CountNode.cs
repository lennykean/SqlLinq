using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlLinq.SyntaxTree.Aggregates
{
    public abstract class CountNode : AggregateNode
    {
        protected CountNode()
        {
        }

        protected override Type EvaluatatorType
        {
            get
            {
                return this.GetType();
            }
        }

        protected override MethodInfo GetEvaluationMethod(Type paramType)
        {
            return (from method in EvaluatatorType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    where method.Name == Name
                    && method.GetParameters().Length == 1
                    select method).First();
        }

        protected override MethodCallExpression GetPropertyOrFieldAggregateExpression(Type tSource, Expression param)
        {
            var lambda = CreateSelector(tSource, param);
            var values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), new[] { tSource, lambda.ReturnType }, param, lambda);

            return Counted(values, lambda.ReturnType);
        }

        internal override Expression GetCallExpression(Type tSource, Expression param)
        {
            return DoNotDereferenceFields ? Counted(param, tSource) : GetPropertyOrFieldAggregateExpression(tSource, param);
        }

        internal virtual bool CountsDistinct
        {
            get
            {
                return false;
            }
        }

        private MethodCallExpression Counted(Expression values, Type type)
        {
            if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null)
            {
                values = Known(values, known => Expression.Call(typeof(Enumerable), nameof(Enumerable.Where), new[] { type }, values, known));
            }

            if (CountsDistinct)
            {
                values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Distinct), new[] { type }, values);
            }

            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), new[] { type }, values);
        }
    }
}
