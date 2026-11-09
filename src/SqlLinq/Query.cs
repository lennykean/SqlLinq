using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree;

namespace SqlLinq
{
    public class Query<TSource, TResult> : QueryBase<TResult>
    {
        private Func<IEnumerable<TSource>, IEnumerable<TResult>> _evaluate;

        public Query(string sql)
            : base(sql)
        {
        }

        public Query(string sql, IEnumerable<ISqlFunction> functions)
            : base(sql, functions)
        {
        }

        internal Expression<Func<IEnumerable<TSource>, IEnumerable<TResult>>> Tree { get; private set; }

        protected override void OnCompile()
        {
            var source = Expression.Parameter(typeof(IEnumerable<TSource>), "source");

            Tree = Expression.Lambda<Func<IEnumerable<TSource>, IEnumerable<TResult>>>(QueryTree.Build(SyntaxNode, source, typeof(TSource), typeof(TResult)), source);
            _evaluate = Tree.Compile();
        }

        public IEnumerable<TResult> Evaluate(IEnumerable<TSource> source)
        {
            Debug.Assert(source != null);
            Debug.Assert(_evaluate != null);

            return _evaluate(source);
        }
    }
}
