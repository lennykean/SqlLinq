using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesPlan<T>
    {
        private readonly Func<IEnumerable<T>, Func<T, TimeSpan>, CancellationToken, IList<IDictionary<string, object>>> _run;

        internal TimeSeriesPlan(TimeSeriesSyntaxTree syntax)
        {
            var tree = new TimeSeriesQueryTree<T>(syntax);
            Syntax = syntax;
            ColumnDescriptions = tree.Columns;
            Columns = ColumnDescriptions.Select(c => c.Name).ToList();
            Expression = tree.Tree;
            _run = Expression.Compile();
        }

        public TimeSeriesSyntaxTree Syntax { get; }

        public IReadOnlyList<string> Columns { get; }

        public IReadOnlyList<TimeSeriesColumn> ColumnDescriptions { get; }

        public Expression<Func<IEnumerable<T>, Func<T, TimeSpan>, CancellationToken, IList<IDictionary<string, object>>>> Expression { get; }

        public IList<IDictionary<string, object>> Execute(IEnumerable<T> source, Func<T, TimeSpan> time)
        {
            return Execute(source, time, CancellationToken.None);
        }

        public IList<IDictionary<string, object>> Execute(IEnumerable<T> source, Func<T, TimeSpan> time, CancellationToken cancellationToken)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            return _run(source, time, cancellationToken);
        }
    }
}
