using System;
using System.Diagnostics;

namespace SqlLinq.SyntaxTree
{
    public class Identifier
    {
        private readonly TerminalNode _terminal;

        private readonly int _start = -1;

        private readonly int _length;

        public Identifier(string id)
        {
            Debug.Assert(id != null);
            LookupId = id;
        }

        public Identifier(string id, int start, int length) : this(id)
        {
            _start = start;
            _length = length;
        }

        internal Identifier(string id, TerminalNode terminal) : this(id)
        {
            _terminal = terminal;
        }

        /// <summary>
        /// The identifier as present in the source text
        /// </summary>
        public string LookupId { get; private set; }

        /// <summary>
        /// In a qualified identifier in the format table.field is the left string
        /// In a non qualified identifier is empty string
        /// </summary>
        public string Qualifier
        {
            get
            {
                if (LookupId.Contains('.'))
                    return LookupId.Substring(0, LookupId.IndexOf(".", StringComparison.CurrentCulture));

                return "";
            }
        }

        /// <summary>
        /// In a qualified identifier in the format table.field is the right string
        /// In a non qualified identifier is SourceId string
        /// </summary>
        public string LocalId
        {
            get
            {
                if (LookupId.Contains('.'))
                    return LookupId.Substring(LookupId.IndexOf(".", StringComparison.CurrentCulture) + 1);

                return LookupId;
            }
        }

        public int Start => _terminal?.Start ?? _start;

        public int Length => _terminal?.Length ?? _length;

        public override string ToString()
        {
            return LookupId;
        }
    }
}
