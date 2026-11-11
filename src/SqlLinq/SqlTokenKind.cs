namespace SqlLinq
{
    public enum SqlTokenKind
    {
        Keyword,
        Identifier,
        BracketedIdentifier,
        StringLiteral,
        NumberLiteral,
        Operator,
        FunctionName,
        Comment,
        Unknown,
    }
}
