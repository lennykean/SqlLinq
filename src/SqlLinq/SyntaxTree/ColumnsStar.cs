using System;
using System.Linq.Expressions;
using System.Diagnostics;

using SqlLinq.SyntaxTree.Aggregates;

namespace SqlLinq.SyntaxTree
{
    [SyntaxNode(RuleConstants.RULE_COLUMNS_TIMES)]
    public class ColumnsStar : Columns
    {
        public ColumnsStar()
        {
        }

        internal override Expression CreateRowResult(ParameterExpression rows, ParameterExpression row, Type tResult)
        {
            var layout = EnclosingSelect().Layout;

            if (layout == null)
            {
                return row;
            }

            return tResult == null ? layout.Star(row) : Expression.Convert(layout.Star(row), tResult);
        }
    }
}
