namespace SqlLinq.SyntaxTree.Joins
{
    internal sealed class JoinedRow<TOuter, TInner>
    {
        public JoinedRow(TOuter outer, TInner inner)
        {
            Outer = outer;
            Inner = inner;
        }

        public TOuter Outer { get; }

        public TInner Inner { get; }
    }
}
