namespace SqlLinq
{
    public enum SqlErrorKind
    {
        Other,
        Lexical,
        Syntax,
        UnterminatedComment,
        UnterminatedString,
        UnbalancedParentheses,
        UnknownName,
        Semantic,
        Evaluation,
    }
}
