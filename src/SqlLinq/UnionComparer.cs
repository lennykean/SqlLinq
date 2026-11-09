using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlLinq
{
    internal sealed class UnionComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<object, object[]> _values;

        public UnionComparer(Func<object, object[]> values)
        {
            _values = values;
        }

        public bool Equals(T x, T y)
        {
            return _values(x).SequenceEqual(_values(y));
        }

        public int GetHashCode(T obj)
        {
            return _values(obj).Aggregate(17, (hash, value) => (hash * 31) + (value?.GetHashCode() ?? 0));
        }
    }
}
