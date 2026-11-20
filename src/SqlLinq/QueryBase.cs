using System;
using System.Collections.Generic;
using System.Diagnostics;

using SqlLinq.SyntaxTree;

namespace SqlLinq
{
    public abstract class QueryBase<TResult>
    {
        protected QueryBase(string sql)
        {
            Debug.Assert(!string.IsNullOrEmpty(sql));
            Sql = sql;
        }

        protected QueryBase(string sql, IEnumerable<ISqlFunction> functions)
            : this(sql)
        {
            _functions = functions;
        }

        private readonly IEnumerable<ISqlFunction> _functions;

        public void Compile()
        {
            var syntax = SqlSyntaxTree.Parse(Sql, _functions);
            if (syntax.Error != null)
                throw syntax.Error;

            if (syntax.Root is UnionNode)
            {
                throw new SqlException("A compiled query can't run a UNION because it runs a single SELECT.", SqlErrorKind.Semantic);
            }

            Syntax = syntax;

            OnCompile();
        }

        protected abstract void OnCompile();

        internal SqlSyntaxTree Syntax { get; private set; }

        public string Sql { get; private set; }

        public override string ToString()
        {
            return Sql;
        }

        protected static IEqualityComparer<TResult> CreateDistinctComparer()
        {
            if (typeof(TResult).IsDictionary())
            {
                Type t = typeof(DictionaryComparer<,>).MakeGenericType(typeof(TResult).GetGenericArguments());

                return (IEqualityComparer<TResult>)Activator.CreateInstance(t);
            }
            else if (typeof(TResult) == typeof(object))
            {
                return (IEqualityComparer<TResult>)new ExpandoComparer();
            }

            return EqualityComparer<TResult>.Default;
        }
    }
}
