using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace SqlLinq
{
    public class JoinQuery<TSource, TInner, TResult> : QueryBase<TResult>
    {
        public JoinQuery(string sql)
            : base(sql)
        {
        }

        protected override void OnCompile()
        {
            if (SyntaxNode.FromClause.Join == null)
                throw new SqlException("The query does not contain a JOIN expression\n" + Sql);
        }

        public IEnumerable<TResult> Evaluate(IEnumerable<TSource> source, IEnumerable<TInner> inner)
        {
            Debug.Assert(source != null);
            Debug.Assert(inner != null);

            return SourcesExtensions.Run<TSource, TResult>(new Dictionary<string, IEnumerable> { ["this"] = source, ["that"] = inner }, Sql, null, true);
        }
    }
}
