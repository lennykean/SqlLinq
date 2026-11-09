using System;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.SyntaxTree.Joins
{
    public abstract class Join : NonTerminalNode
    {
        private Lazy<Identifier> _leftId;
        private Lazy<Identifier> _rightId;

        protected Join()
        {
            _leftId = new Lazy<Identifier>(() => SqlSyntax.Name(FindChild<TerminalNode>(4).Text));
            _rightId = new Lazy<Identifier>(() => SqlSyntax.Name(FindChild<TerminalNode>(6).Text));
            //Enumerable.Join(,,,,)
        }

        public string OuterName
        {
            get
            {
                return this.FindChild<NodeWithId>().Id.LookupId;
            }
        }

#pragma warning disable CA1309 // keeps upstream's culture-sensitive comparison
        public string OuterKey
        {
            get
            {
                if (RightId.Qualifier.Equals(OuterName, StringComparison.CurrentCultureIgnoreCase) == false)
                    return RightId.LocalId;

                return LeftId.LocalId;
            }
        }

        public string InnerKey
        {
            get
            {
                if (RightId.Qualifier.Equals(OuterName, StringComparison.CurrentCultureIgnoreCase))
                    return RightId.LocalId;

                return LeftId.LocalId;
            }
        }
#pragma warning restore CA1309

        public Identifier LeftId
        {
            get
            {
                return _leftId.Value;
            }
        }

        public Identifier RightId
        {
            get
            {
                return _rightId.Value;
            }
        }
    }
}
