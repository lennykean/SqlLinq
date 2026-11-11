using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace SqlLinq
{
    public sealed class SqlPlan<TResult>
    {
        private readonly IReadOnlyList<string> _tables;

        private readonly Func<IEnumerable<IEnumerable>, IEnumerable<TResult>> _run;

        internal SqlPlan(SqlSyntaxTree syntax, IReadOnlyList<string> tables, IReadOnlyList<string> columns, Expression<Func<IEnumerable<IEnumerable>, IEnumerable<TResult>>> expression)
        {
            Syntax = syntax;
            _tables = tables;
            Columns = columns;
            Expression = expression;
            _run = expression.Compile();
        }

        public SqlSyntaxTree Syntax { get; }

        public Expression<Func<IEnumerable<IEnumerable>, IEnumerable<TResult>>> Expression { get; }

        public IReadOnlyList<string> Columns { get; }

        public IEnumerable<TResult> Execute(IEnumerable source)
        {
            if (_tables.Count != 1)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The plan reads {0} tables; execute it with them by name.", _tables.Count));
            }

            return Run(source ?? Array.Empty<object>());
        }

        internal IEnumerable<TResult> Run(IEnumerable source)
        {
            return _run(new[] { source });
        }

        public IEnumerable<TResult> Execute(IDictionary<string, IEnumerable> tables)
        {
            return Execute(tables, CancellationToken.None);
        }

        public IEnumerable<TResult> Execute(IDictionary<string, IEnumerable> tables, CancellationToken cancellationToken)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var sources = cancellationToken.CanBeCanceled ? SourcesExtensions.Cancellable(tables, cancellationToken) : tables;

            return _run(_tables.Select(name => Table(sources, name)).ToArray());
        }

        private static IEnumerable Table(IDictionary<string, IEnumerable> tables, string name)
        {
            if (!tables.TryGetValue(name, out var table))
            {
                var match = tables.FirstOrDefault(t => string.Equals(t.Key, name, StringComparison.OrdinalIgnoreCase));
                if (match.Key == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "The plan reads a table named '{0}', which tables doesn't have.", name), nameof(tables));
                }

                table = match.Value;
            }

            return table ?? Array.Empty<object>();
        }
    }
}
