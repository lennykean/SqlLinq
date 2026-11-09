using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using SqlLinq.SyntaxTree;

namespace SqlLinq
{
    internal interface ICompile
    {
        void Compile(SelectNode syntaxNode);
    }

    public abstract class QueryBase<TResult> : ICompile
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

        void ICompile.Compile(SelectNode syntaxNode)
        {
            SyntaxNode = syntaxNode;
            OnCompile();
        }

        public void Compile()
        {
            SqlParser parser = new SqlParser(_functions);
            if (!parser.Parse(Sql))
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin statement\n\t{1}", parser.ErrorString, parser.ErrorLine));

            if (parser.SyntaxTree is UnionNode)
            {
                throw new SqlException("A compiled query can't run a UNION; use the Query extension methods instead.");
            }

            SyntaxNode = parser.SyntaxTree as SelectNode;
            Debug.Assert(SyntaxNode != null);

            OnCompile();
        }

        protected abstract void OnCompile();

        internal SelectNode SyntaxNode { get; private set; }

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
