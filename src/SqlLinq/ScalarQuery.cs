using System;
using System.Collections.Generic;

using SqlLinq.SyntaxTree;

namespace SqlLinq
{
    public class ScalarQuery<TSource, TResult> : QueryBase<TResult>
    {
        public ScalarQuery(string sql)
            : base(sql)
        {
        }

        public Func<IEnumerable<TSource>, TResult> Select { get; private set; }

        protected override void OnCompile()
        {
            var plan = Syntax.Compile<TSource, object>(true);

            Select = ((SelectNode)Syntax.Root).Columns.CreateScalarSelector<TSource, TResult>(source => plan.Run(source));
        }

        public TResult Evaluate(IEnumerable<TSource> source)
        {
            return Select(source);
        }
    }
}
