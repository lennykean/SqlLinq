using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SqlLinq
{
    public class Query<TSource, TResult> : QueryBase<TResult>
    {
        private SqlPlan<TResult> _plan;

        public Query(string sql)
            : base(sql)
        {
        }

        public Query(string sql, IEnumerable<ISqlFunction> functions)
            : base(sql, functions)
        {
        }

        protected override void OnCompile()
        {
            _plan = Syntax.Compile<TSource, TResult>(true);
        }

        public IEnumerable<TResult> Evaluate(IEnumerable<TSource> source)
        {
            Debug.Assert(source != null);
            if (_plan == null)
            {
                throw new InvalidOperationException("A query can't be evaluated until it has been compiled.");
            }

            return _plan.Run(source);
        }
    }
}
