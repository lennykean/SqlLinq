using System;
using System.Collections;
using System.Collections.Generic;

using SqlLinq.SyntaxTree;

namespace SqlLinq
{
    public class ScalarJoinQuery<TSource, TInner, TResult> : QueryBase<TResult>
    {
        public ScalarJoinQuery(string sql)
            : base(sql)
        {
        }

        internal Func<IEnumerable<TSource>, IEnumerable<TInner>, TResult> Select { get; private set; }

        protected override void OnCompile()
        {
            Select = ((SelectNode)Syntax.Root).Columns.CreateScalarSelector<TSource, TInner, TResult>(Rows);
        }

        public TResult Evaluate(IEnumerable<TSource> source, IEnumerable<TInner> inner)
        {
            if (Select == null)
            {
                throw new InvalidOperationException("A query can't be evaluated until it has been compiled.");
            }

            return Select(source, inner);
        }

        private IEnumerable<object> Rows(IEnumerable<TSource> source, IEnumerable<TInner> inner)
        {
            return SourcesExtensions.Run<TSource, object>(new Dictionary<string, IEnumerable> { ["this"] = source, ["that"] = inner }, Sql, null, true);
        }
    }
}
