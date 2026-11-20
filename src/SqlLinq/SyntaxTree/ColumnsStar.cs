using System;
using System.Linq;
using System.Linq.Expressions;

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

            if (layout == null && tResult != null && tResult.IsDictionary() && !tResult.IsAssignableFrom(row.Type) && row.Type != typeof(object) && !row.Type.IsDictionary())
            {
                var names = row.Type.ColumnNames().ToList();

                return ExpressionFactory.CreateResult(tResult, names, names.Select(name => ExpressionFactory.CreateFieldSelector(row, name)).ToList());
            }

            if (layout == null)
            {
                return row;
            }

            return tResult == null ? layout.Star(row) : Expression.Convert(layout.Star(row), tResult);
        }
    }
}
