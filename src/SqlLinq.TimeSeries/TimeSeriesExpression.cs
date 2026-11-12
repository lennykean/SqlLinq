using System;
using System.Collections.Generic;
using System.Linq;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesExpression
    {
        private TimeSeriesExpression _original;

        internal TimeSeriesExpression(string text, string alias, string name, int start, SqlSyntaxTree syntax)
        {
            Text = text;
            Alias = alias;
            Name = name;
            Start = start;
            Syntax = syntax;
            Expression = syntax.Root as ExpressionNode;
            Columns = new[] { name };
        }

        public string Text { get; }

        public string Alias { get; }

        public int Start { get; private set; }

        public ExpressionNode Expression { get; private set; }

        public IReadOnlyList<NodeWithId> Aggregates { get; internal set; } = Array.Empty<NodeWithId>();

        public IReadOnlyList<string> Columns { get; internal set; }

        public IReadOnlyList<Identifier> Names => Syntax.Names.Concat(CaseTrees.SelectMany(t => t.Names)).ToList();

        public TimeSeriesExpression Original => _original ?? this;

        internal string Name { get; }

        internal SqlSyntaxTree Syntax { get; private set; }

        internal SqlException Error { get; set; }

        internal bool Whole { get; set; }

        internal bool Plain { get; set; }

        internal IReadOnlyList<string> CaseLabels { get; set; }

        internal IReadOnlyList<SqlSyntaxTree> CaseTrees { get; set; } = Array.Empty<SqlSyntaxTree>();

        internal IReadOnlyList<ExpressionNode> Conditions { get; set; } = Array.Empty<ExpressionNode>();

        internal IReadOnlyList<string> Labels { get; set; }

        internal double[] Boundaries { get; set; }

        internal bool Facet { get; set; }

        internal string Written { get; set; }

        internal string Source { get; set; }

        internal TimeSeriesExpression Rewrite(ExpressionNode expression, IReadOnlyList<NodeWithId> aggregates, IReadOnlyList<ExpressionNode> conditions)
        {
            var rewritten = Copy();
            rewritten.Expression = expression;
            rewritten.Aggregates = aggregates;
            rewritten.Conditions = conditions;
            rewritten._original = this;

            return rewritten;
        }

        internal TimeSeriesExpression Bind(IReadOnlyList<ISqlFunction> functions)
        {
            var bound = Copy();
            bound.Expression = (ExpressionNode)Expression?.Bind(functions);
            bound.Aggregates = Aggregates.Select(a => (NodeWithId)a.Bind(functions)).ToList();
            bound.Conditions = Conditions.Select(c => (ExpressionNode)c?.Bind(functions)).ToList();

            return bound;
        }

        internal TimeSeriesExpression Shift(string source, int offset)
        {
            var shifted = Copy();
            shifted.Start = Start + offset;
            shifted.Syntax = Syntax.Shift(source, offset);
            shifted.Expression = shifted.Syntax.Root as ExpressionNode;
            shifted.CaseTrees = CaseTrees.Select(t => t.Shift(source, offset)).ToList();
            shifted.Conditions = CaseLabels == null ? Conditions : shifted.CaseTrees.Select(t => (ExpressionNode)t.Root).ToList();
            shifted.Error = Error == null || Error.Start < 0 ? Error : new SqlException(Error.Message, Error.InnerException, Error.Kind, Error.Start + offset, Error.Length);
            shifted.Source = source;

            return shifted;
        }

        internal TimeSeriesExpression Copy()
        {
            return (TimeSeriesExpression)MemberwiseClone();
        }
    }
}
