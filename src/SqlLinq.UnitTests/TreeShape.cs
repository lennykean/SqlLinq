using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace SqlLinq.UnitTests
{
    internal sealed class TreeShape : ExpressionVisitor
    {
        private static readonly HashSet<ExpressionType> Allowed =
        [
            ExpressionType.Lambda,
            ExpressionType.Parameter,
            ExpressionType.MemberAccess,
            ExpressionType.Constant,
            ExpressionType.Convert,
            ExpressionType.ConvertChecked,
            ExpressionType.Add,
            ExpressionType.AddChecked,
            ExpressionType.Subtract,
            ExpressionType.SubtractChecked,
            ExpressionType.Multiply,
            ExpressionType.MultiplyChecked,
            ExpressionType.Divide,
            ExpressionType.Modulo,
            ExpressionType.Negate,
            ExpressionType.NegateChecked,
            ExpressionType.UnaryPlus,
            ExpressionType.Equal,
            ExpressionType.NotEqual,
            ExpressionType.LessThan,
            ExpressionType.LessThanOrEqual,
            ExpressionType.GreaterThan,
            ExpressionType.GreaterThanOrEqual,
            ExpressionType.AndAlso,
            ExpressionType.OrElse,
            ExpressionType.And,
            ExpressionType.Or,
            ExpressionType.ExclusiveOr,
            ExpressionType.Not,
            ExpressionType.Conditional,
            ExpressionType.New,
            ExpressionType.MemberInit,
            ExpressionType.NewArrayInit,
            ExpressionType.NewArrayBounds,
            ExpressionType.Call,
            ExpressionType.Throw,
        ];

        private readonly List<string> _disallowed = [];

        internal static string Disallowed(Expression tree)
        {
            var shape = new TreeShape();
            shape.Visit(tree);

            return string.Join("; ", shape._disallowed.Distinct());
        }

        public override Expression Visit(Expression node)
        {
            if (node != null && !Allowed.Contains(node.NodeType))
            {
                _disallowed.Add(node.NodeType.ToString());
            }

            return base.Visit(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;
            var allowed = method.DeclaringType == typeof(Enumerable)
                || method.DeclaringType.Namespace == "LinqStatistics"
                || method.DeclaringType == typeof(string) && method.Name == nameof(string.CompareOrdinal)
                || method.DeclaringType == typeof(Regex) && method.Name == nameof(Regex.IsMatch);
            if (!allowed)
            {
                _disallowed.Add("call " + method.DeclaringType.Name + "." + method.Name);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Throw && !TooManyRows(node.Operand))
            {
                _disallowed.Add("throw " + node.Operand);
            }

            return base.VisitUnary(node);
        }

        private static bool TooManyRows(Expression error)
        {
            return error is NewExpression create
                && create.Type == typeof(SqlException)
                && create.Arguments.Count == 1
                && create.Arguments[0] is ConstantExpression message
                && Equals(message.Value, "The subquery returned more than one row.");
        }
    }
}
