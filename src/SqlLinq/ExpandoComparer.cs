using System.Collections.Generic;

namespace SqlLinq
{
    class ExpandoComparer : EqualityComparer<object>
    {
        private DictionaryComparer<string, object> _comparer = new DictionaryComparer<string, object>();

        public override bool Equals(object x, object y)
        {
            if (!(x is IDictionary<string, object>) && !(y is IDictionary<string, object>))
            {
                return object.Equals(SourcesExtensions.KeyOf(x), SourcesExtensions.KeyOf(y));
            }

            return _comparer.Equals(x as IDictionary<string, object>, y as IDictionary<string, object>);
        }

        public override int GetHashCode(object obj)
        {
            return obj is IDictionary<string, object> || obj == null ? _comparer.GetHashCode(obj as IDictionary<string, object>) : SourcesExtensions.KeyOf(obj).GetHashCode();
        }
    }
}
