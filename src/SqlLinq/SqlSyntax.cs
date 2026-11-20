using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using GoldParser;

using SqlLinq.SyntaxTree;

namespace SqlLinq
{
    public static class SqlSyntax
    {
        private static readonly Regex CallStart = new Regex(@"'[^']*'|\[[^\]]*\]|--[^\r\n]*|/\*[\s\S]*?(?:\*/|\z)|(?<!\.)\b(\w+)\s*\(");

        private static readonly Regex Bracketed = new Regex(@"\[([^\]]*)\]");

        private static readonly Regex BracketedParts = new Regex(@"^\[([^\]]+)\]\.\[([^\]]+)\]$");

        private static readonly char[] LineBreaks = { '\r', '\n' };

        public static IReadOnlyList<string> Keywords { get; } = Array.AsReadOnly(new[] { "ALL", "AND", "ANY", "AS", "ASC", "BETWEEN", "BY", "DESC", "DISTINCT", "ESCAPE", "EXISTS", "FALSE", "FROM", "GROUP", "HAVING", "IN", "INNER", "IS", "JOIN", "LEFT", "LIKE", "NOT", "NULL", "ON", "OR", "ORDER", "RIGHT", "SELECT", "SOME", "TRUE", "UNION", "WHERE" });

        public static IReadOnlyList<string> Aggregates { get; } = Array.AsReadOnly(new[] { "Avg", "Count", "Max", "Min", "StDev", "StDevP", "Sum", "Var", "VarP" });

        public static IReadOnlyList<string> Functions { get; } = Array.AsReadOnly(MathFunction.All.Select(f => f.Name).ToArray());

        public static IReadOnlyList<SqlToken> Tokenize(string text)
        {
            var tokens = new List<SqlToken>();
            var lexer = ParserFactory.CreateParser(new StringReader(text));
            var next = 0;

            for (var symbol = lexer.ReadToken(); symbol.SymbolType != SymbolType.End; symbol = lexer.ReadToken())
            {
                var start = lexer.TokenCharPosition;
                if (start < next || symbol.SymbolType == SymbolType.WhiteSpace)
                {
                    continue;
                }

                var kind = KindOf(lexer, text);
                next = start + lexer.TokenLength;
                if (symbol.SymbolType == SymbolType.CommentStart)
                {
                    next = CommentEnd(lexer, text.Length);
                }
                else if (symbol.SymbolType == SymbolType.CommentLine || (kind == SqlTokenKind.StringLiteral && symbol.SymbolType == SymbolType.Error))
                {
                    var end = text.IndexOfAny(LineBreaks, start);
                    next = end < 0 ? text.Length : end;
                }

                tokens.Add(new SqlToken(kind, text.Substring(start, next - start), start));
            }

            return tokens;
        }

        internal static List<string> SplitTopLevel(string text)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            var quoted = false;
            var bracketed = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    quoted = c != '\'';
                }
                else if (bracketed)
                {
                    bracketed = c != ']';
                }
                else if (c == '\'')
                {
                    quoted = true;
                }
                else if (c == '[')
                {
                    bracketed = true;
                }
                else if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    parts.Add(text[start..i].Trim());
                    start = i + 1;
                }
            }

            parts.Add(text[start..].Trim());

            return parts;
        }

        internal static List<FunctionCall> FindCalls(string text, ICollection<string> names)
        {
            var calls = new List<FunctionCall>();
            var i = 0;

            Match match;

            while ((match = CallStart.Match(text, i)).Success)
            {
                if (!match.Groups[1].Success || !names.Contains(match.Groups[1].Value))
                {
                    i = match.Index + match.Length;
                    continue;
                }

                var open = match.Index + match.Length - 1;
                var close = ClosingParen(text, open);

                if (close < 0)
                {
                    throw new SqlException("The query can't be read because a function call has no closing parenthesis.", SqlErrorKind.UnbalancedParentheses, match.Index, text.Length - match.Index);
                }
                calls.Add(new FunctionCall(match.Index, close, match.Groups[1].Value, text.Substring(open + 1, close - open - 1)));

                i = close + 1;
            }

            return calls;
        }

        internal static string ReplaceCalls(string text, List<FunctionCall> calls, Func<int, string> replacement)
        {
            var sb = new StringBuilder();
            var i = 0;

            for (var n = 0; n < calls.Count; n++)
            {
                sb
                    .Append(text, i, calls[n].Start - i)
                    .Append(replacement(n));

                i = calls[n].End + 1;
            }

            return sb.Append(text, i, text.Length - i).ToString();
        }

        public static string Unbracket(string text)
        {
            var parts = BracketedParts.Match(text);
            if (parts.Success)
            {
                return parts.Groups[1].Value + "." + parts.Groups[2].Value;
            }

            return text.Length > 1 && text[0] == '[' && text[text.Length - 1] == ']' ? text[1..^1] : text;
        }

        internal static Identifier Name(string text)
        {
            return new Identifier(Bracketed.Replace(text, "$1"));
        }

        internal static Identifier Name(TerminalNode terminal)
        {
            return terminal == null ? Name(string.Empty) : new Identifier(Bracketed.Replace(terminal.Text, "$1"), terminal);
        }

        public static string Unquote(string text)
        {
            if (text.Length < 2 || text[0] != '\'' || text[text.Length - 1] != '\'')
            {
                throw new SqlException("The text can't be unquoted because it isn't a quoted string: " + text, SqlErrorKind.Syntax);
            }

            return text[1..^1];
        }

        private static SqlTokenKind KindOf(Parser lexer, string text)
        {
            switch (lexer.TokenSymbol.SymbolType)
            {
                case SymbolType.CommentLine:
                case SymbolType.CommentStart:
                    return SqlTokenKind.Comment;

                case SymbolType.Error:
                    return text[lexer.TokenCharPosition] == '\'' ? SqlTokenKind.StringLiteral : SqlTokenKind.Unknown;

                case SymbolType.CommentEnd:
                    return SqlTokenKind.Unknown;
            }

            switch ((SymbolConstants)lexer.TokenSymbol.Index)
            {
                case SymbolConstants.SYMBOL_ID:
                    if (lexer.TokenText.Contains('['))
                    {
                        return SqlTokenKind.BracketedIdentifier;
                    }

                    var after = lexer.TokenCharPosition + lexer.TokenLength;
                    while (after < text.Length && char.IsWhiteSpace(text[after]))
                    {
                        after++;
                    }

                    return after < text.Length && text[after] == '(' ? SqlTokenKind.FunctionName : SqlTokenKind.Identifier;

                case SymbolConstants.SYMBOL_STRINGLITERAL:
                case SymbolConstants.SYMBOL_DATELITERAL:
                    return SqlTokenKind.StringLiteral;

                case SymbolConstants.SYMBOL_INTEGERLITERAL:
                case SymbolConstants.SYMBOL_REALLITERAL:
                    return SqlTokenKind.NumberLiteral;

                case SymbolConstants.SYMBOL_FUNCTION:
                case SymbolConstants.SYMBOL_AVG:
                case SymbolConstants.SYMBOL_COUNT:
                case SymbolConstants.SYMBOL_MAX:
                case SymbolConstants.SYMBOL_MIN:
                case SymbolConstants.SYMBOL_STDEV:
                case SymbolConstants.SYMBOL_STDEVP:
                case SymbolConstants.SYMBOL_SUM:
                case SymbolConstants.SYMBOL_VAR:
                case SymbolConstants.SYMBOL_VARP:
                    return SqlTokenKind.FunctionName;

                default:
                    return char.IsLetter(lexer.TokenSymbol.Name[0]) ? SqlTokenKind.Keyword : SqlTokenKind.Operator;
            }
        }

        private static int CommentEnd(Parser lexer, int end)
        {
            for (var depth = 1; depth > 0;)
            {
                switch (lexer.ReadToken().SymbolType)
                {
                    case SymbolType.CommentStart:
                        depth++;
                        break;

                    case SymbolType.CommentEnd:
                        depth--;
                        break;

                    case SymbolType.End:
                        return end;
                }
            }

            return lexer.TokenCharPosition + lexer.TokenLength;
        }

        private static int ClosingParen(string text, int open)
        {
            var depth = 0;
            var quoted = false;
            var quote = -1;
            var bracketed = false;

            for (var i = open; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    quoted = c != '\'';
                }
                else if (bracketed)
                {
                    bracketed = c != ']';
                }
                else if (c == '\'')
                {
                    quoted = true;
                    quote = i;
                }
                else if (c == '[')
                {
                    bracketed = true;
                }
                else if (c == '(')
                {
                    depth++;
                }
                else if (c == ')' && --depth == 0)
                {
                    return i;
                }
            }

            if (quoted)
            {
                throw new SqlException("The query can't be read because a string has no closing quote.", SqlErrorKind.UnterminatedString, quote, 1);
            }

            return -1;
        }
    }
}
