using System;
using System.Collections.Generic;
using System.Linq;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesExpression
    {
        internal TimeSeriesExpression(string text, string alias, string name, int start, SqlSyntaxTree syntax)
        {
            Text = text;
            Alias = alias;
            Name = name;
            Start = start;
            Syntax = syntax;
            Expression = syntax.Root as ExpressionNode;
        }

        public string Text { get; }

        public string Alias { get; }

        public int Start { get; }

        public ExpressionNode Expression { get; private set; }

        public IReadOnlyList<NodeWithId> Aggregates { get; internal set; } = Array.Empty<NodeWithId>();

        internal string Name { get; }

        internal SqlSyntaxTree Syntax { get; }

        internal SqlException Error { get; set; }

        internal bool Whole { get; set; }

        internal bool Plain { get; set; }

        internal IReadOnlyList<string> CaseLabels { get; set; }

        internal IReadOnlyList<SqlSyntaxTree> CaseTrees { get; set; } = Array.Empty<SqlSyntaxTree>();

        internal IReadOnlyList<ExpressionNode> Conditions { get; set; } = Array.Empty<ExpressionNode>();

        internal IReadOnlyList<string> Labels { get; set; }

        internal double[] Boundaries { get; set; }

        internal IEnumerable<Identifier> Names => Syntax.Names.Concat(CaseTrees.SelectMany(t => t.Names));

        internal TimeSeriesExpression Rewrite(ExpressionNode expression, IReadOnlyList<NodeWithId> aggregates, IReadOnlyList<ExpressionNode> conditions)
        {
            var rewritten = (TimeSeriesExpression)MemberwiseClone();
            rewritten.Expression = expression;
            rewritten.Aggregates = aggregates;
            rewritten.Conditions = conditions;

            return rewritten;
        }
    }
}
