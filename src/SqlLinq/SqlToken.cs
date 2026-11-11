namespace SqlLinq
{
    public sealed class SqlToken
    {
        public SqlToken(SqlTokenKind kind, string text, int start)
        {
            Kind = kind;
            Text = text;
            Start = start;
        }

        public SqlTokenKind Kind { get; }

        public string Text { get; }

        public int Start { get; }
    }
}
