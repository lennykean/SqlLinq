using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_ORDERLIST_INTEGERLITERAL_COMMA)]
    [SyntaxNode(RuleConstants.RULE_ORDERLIST_INTEGERLITERAL)]
    internal sealed class OrderByPositionItem : OrderByItem
    {
        internal string Position
        {
            get
            {
                return GetTerminalText("IntegerLiteral");
            }
        }

        internal override Identifier CreateIdentifier()
        {
            return new Identifier(Position);
        }

        internal override ColumnSource SelectColumn()
        {
            var columns = EnclosingSelect().Columns;

            return columns is ColumnsStar ? null : Column(columns.ColumnSources.ToList());
        }

        protected override LambdaExpression CreateLambaExpression(Type tSource)
        {
            if (!(EnclosingSelect().Columns is ColumnsStar))
            {
                return base.CreateLambaExpression(tSource);
            }

            var fields = tSource == typeof(object) || tSource.IsDictionary() || EnclosingSelect().Layout != null ? new List<string>() : tSource.ColumnNames().ToList();
            if (fields.Count == 0)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY {0} needs a select list; SELECT * over dictionary or dynamic rows has no fixed column order.", Position), SqlErrorKind.Semantic);
            }

            return ExpressionFactory.CreateFieldSelectorLambda(tSource, Column(fields));
        }

        internal T Column<T>(List<T> columns)
        {
            if (!int.TryParse(Position, NumberStyles.None, CultureInfo.InvariantCulture, out var position) || position < 1 || position > columns.Count)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "ORDER BY {0} is out of range; the select list has {1} columns.", Position, columns.Count), SqlErrorKind.Semantic);
            }

            return columns[position - 1];
        }
    }
}
