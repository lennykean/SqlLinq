using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Joins;

namespace SqlLinq.SyntaxTree
{
    internal sealed class DereferencedRows : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _rows = new HashSet<ParameterExpression>();

        private DereferencedRows()
        {
        }

        internal static IEnumerable<ParameterExpression> In(Expression expression)
        {
            var visitor = new DereferencedRows();
            visitor.Visit(expression);

            return FreeVariables.Of(expression).Where(visitor._rows.Contains).ToList();
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression row && !row.Type.IsValueType && !Joined(row))
            {
                _rows.Add(row);
            }

            return base.VisitMember(node);
        }

        private static bool Joined(ParameterExpression row)
        {
            return row.Type.IsGenericType && row.Type.GetGenericTypeDefinition() == typeof(JoinedRow<,>);
        }
    }
}
