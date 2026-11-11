using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq
{
    internal sealed class TableSlot : IEnumerable
    {
        private readonly ParameterExpression _tables;

        private readonly int _index;

        internal TableSlot(ParameterExpression tables, int index, Type elementType)
        {
            _tables = tables;
            _index = index;
            ElementType = elementType;
        }

        internal Type ElementType { get; }

        internal static Expression Table(ParameterExpression tables, int index)
        {
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.ElementAt), new[] { typeof(IEnumerable) }, tables, Expression.Constant(index));
        }

        internal Expression Rows(Type type)
        {
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Cast), new[] { type }, Table(_tables, _index));
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotSupportedException("A plan reads its tables when it executes.");
        }
    }
}
