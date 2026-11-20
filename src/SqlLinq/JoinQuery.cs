using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using SqlLinq.SyntaxTree;

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
            if (((SelectNode)Syntax.Root).FromClause.Join == null)
                throw new SqlException("The query can't join two sources without a JOIN expression\n" + Sql, SqlErrorKind.Semantic);
        }

        public IEnumerable<TResult> Evaluate(IEnumerable<TSource> source, IEnumerable<TInner> inner)
        {
            Debug.Assert(source != null);
            Debug.Assert(inner != null);

            return SourcesExtensions.Run<TSource, TResult>(new Dictionary<string, IEnumerable> { ["this"] = source, ["that"] = inner }, Sql, null, true);
        }
    }
}
