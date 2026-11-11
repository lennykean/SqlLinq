using System;
using System.Collections.Generic;
using System.Threading;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    public class TimeSeriesQuery<T>
    {
        private readonly Func<T, TimeSpan> _time;

        public TimeSeriesQuery(string nrql, Func<T, TimeSpan> time, Func<ExpressionNode, ExpressionNode> rewrite = null) : this(nrql, time, rewrite, null)
        {
        }

        public TimeSeriesQuery(string nrql, Func<T, TimeSpan> time, Func<ExpressionNode, ExpressionNode> rewrite, IEnumerable<ISqlFunction> functions)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            Plan = TimeSeriesSyntaxTree.Parse(nrql, functions).Rewrite(rewrite).Compile<T>();
        }

        public TimeSeriesPlan<T> Plan { get; }

        public IList<IDictionary<string, object>> Evaluate(IEnumerable<T> source)
        {
            return Evaluate(source, CancellationToken.None);
        }

        public IList<IDictionary<string, object>> Evaluate(IEnumerable<T> source, CancellationToken cancellationToken)
        {
            return Plan.Execute(source, _time, cancellationToken);
        }
    }
}
