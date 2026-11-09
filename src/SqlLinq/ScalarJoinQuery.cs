using System;
using System.Collections;
using System.Collections.Generic;

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
            Select = SyntaxNode.Columns.CreateScalarSelector<TSource, TInner, TResult>(Rows);
        }

        public TResult Evaluate(IEnumerable<TSource> source, IEnumerable<TInner> inner)
        {
            return Select(source, inner);
        }

        private IEnumerable<object> Rows(IEnumerable<TSource> source, IEnumerable<TInner> inner)
        {
            return SourcesExtensions.Run<TSource, object>(new Dictionary<string, IEnumerable> { ["this"] = source, ["that"] = inner }, Sql, null, true);
        }
    }
}
