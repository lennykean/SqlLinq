using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq
{
    public static class SqlExpression
    {
        public static Func<T, object> Compile<T>(string expression)
        {
            return Compile<T>(expression, null);
        }

        public static Func<T, object> Compile<T>(string expression, IEnumerable<ISqlFunction> functions)
        {
            try
            {
                var syntax = SqlSyntaxTree.ParseExpression(expression, functions);
                if (syntax.Error != null)
                {
                    throw syntax.Error;
                }

                return Built<T>((ExpressionNode)syntax.Root).Compile();
            }
            catch (Exception e) when (!(e is SqlException))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL compile error:\n\t{0}\nin expression\n\t{1}", e.Message, expression), e, Kind(e));
            }
        }

        public static Func<T, object> Compile<T>(ExpressionNode expression)
        {
            return Wrapped(expression, () => Built<T>(expression).Compile());
        }

        public static Expression<Func<T, object>> Lambda<T>(ExpressionNode expression)
        {
            return Wrapped(expression, () => Built<T>(expression));
        }

        private static TCompiled Wrapped<TCompiled>(ExpressionNode expression, Func<TCompiled> build)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            try
            {
                return build();
            }
            catch (Exception e) when (!(e is SqlException))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL compile error:\n\t{0}\nin expression\n\t{1}", e.Message, expression.Source ?? expression.ToSql()), e, Kind(e), expression.Start, expression.Length);
            }
        }

        private static Expression<Func<T, object>> Built<T>(ExpressionNode expression)
        {
            var error = expression.FindDescendants<NodeWithId>().Prepend(expression as NodeWithId)
                .SelectMany(n => n?.Arguments ?? Enumerable.Empty<ArgumentNode>())
                .Select(a => a.Error)
                .FirstOrDefault(e => e != null);
            if (error != null)
            {
                throw error;
            }

            var subquery = expression.Subqueries().FirstOrDefault();
            if (subquery != null)
            {
                throw new SqlException("The subquery can't be evaluated because the expression has no table for it to read.", SqlErrorKind.Semantic, subquery.Start, subquery.Length);
            }

            var node = (ExpressionNode)expression.Copy();
            var source = Expression.Parameter(typeof(IEnumerable<T>), "source");
            var item = Expression.Parameter(typeof(T), "item");
            var body = Expression.Convert(node.CreateExpression(source, item), typeof(object));

            return Expression.Lambda<Func<T, object>>(ParameterReplacer.Replace(body, source, Expression.Constant(Enumerable.Empty<T>(), source.Type)), item);
        }

        private static SqlErrorKind Kind(Exception e)
        {
            return e.Data[nameof(SqlErrorKind)] is SqlErrorKind kind ? kind : SqlErrorKind.Semantic;
        }
    }
}
