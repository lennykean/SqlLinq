using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Text;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_LIKE_STRINGLITERAL)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_LIKE_STRINGLITERAL_ESCAPE_STRINGLITERAL)]
    public class LikeNode : ExpressionNode
    {
        public LikeNode()
        {
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            MethodInfo matchMethod = typeof(LikeNode).GetMethod("IsRegexMatch", new Type[] { typeof(object), typeof(Regex) });

            var literals = Children.OfType<TerminalNode>().Where(t => t.Symbol.Name == "StringLiteral").ToList();
            string like = SqlSyntax.Unquote(literals[0].Text);
            Regex regex;
            try
            {
                regex = new Regex(literals.Count == 1 ? ConvertLikeToRegex(like) : ConvertLikeToRegex(like, Escape(literals[1])), RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch (SqlException e) when (e.Start < 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, literals[0].Start, literals[0].Length);
            }

            Expression match = Expression.Constant(regex);
            var input = CreateChildExpression(sourceData, param, 0);
            if (input.Type == typeof(string))
            {
                return PredicateExpressionNode.Known(Expression.Call(match, typeof(Regex).GetMethod(nameof(Regex.IsMatch), new[] { typeof(string) }), input), input);
            }

            var value = input.Type == typeof(object) ? input : Expression.Convert(input, typeof(object));

            return PredicateExpressionNode.Known(Expression.Call(matchMethod, value, match), value);
        }

        public static bool IsRegexMatch(object input, Regex regex)
        {
            if (input == null)
                return false;

            return regex.IsMatch(input.ToString());
        }

        internal static bool IsRegexMatch(string input, string regex)
        {
            if (input == null)
                return false;

            return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        internal static string ConvertLikeToRegex(string pattern)
        {
            return WithoutBacktracking("^" + ConvertWildcards(pattern) + "$");
        }

        internal static string ConvertLikeToRegex(string pattern, char escape)
        {
            var builder = new StringBuilder("^");
            var run = new StringBuilder();

            for (var i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] != escape)
                {
                    run.Append(pattern[i]);
                    continue;
                }

                if (i == pattern.Length - 1)
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "The LIKE pattern '{0}' can't end with its escape character '{1}'.", pattern, escape), SqlErrorKind.Semantic);
                }

                builder.Append(ConvertWildcards(run.ToString())).Append(Regex.Escape(pattern[++i].ToString()));
                run.Clear();
            }

            return WithoutBacktracking(builder.Append(ConvertWildcards(run.ToString())).Append('$').ToString());
        }

        private static string WithoutBacktracking(string regex)
        {
            var segments = new List<string>();
            var segment = new StringBuilder();
            for (var i = 0; i < regex.Length; i++)
            {
                if (regex[i] == '\\' && i + 1 < regex.Length)
                {
                    segment.Append(regex, i++, 2);
                }
                else if (regex[i] == '[' && regex[ClassEnd(regex, i)] != ']' || regex[i] == '^' && i > 0)
                {
                    segment.Append('\\').Append(regex[i]);
                }
                else if (regex[i] == '[')
                {
                    var end = ClassEnd(regex, i);
                    segment.Append(regex, i, end - i + 1);
                    i = end;
                }
                else if (regex[i] == '.' && i + 1 < regex.Length && regex[i + 1] == '*')
                {
                    segments.Add(segment.ToString());
                    segment.Clear();
                    i++;
                }
                else
                {
                    segment.Append(regex[i]);
                }
            }

            segments.Add(segment.ToString());
            if (segments.Count < 3)
            {
                return string.Join(".*", segments);
            }

            var atomic = new StringBuilder(segments[0]);
            for (var i = 1; i < segments.Count - 1; i++)
            {
                atomic.Append("(?>.*?").Append(segments[i]).Append(')');
            }

            return atomic.Append(".*").Append(segments[segments.Count - 1]).ToString();
        }

        private static int ClassEnd(string regex, int start)
        {
            var i = start + 1;
            if (i < regex.Length && regex[i] == '^')
            {
                i++;
            }

            if (i < regex.Length && regex[i] == ']')
            {
                i++;
            }

            while (i < regex.Length && regex[i] != ']')
            {
                i += regex[i] == '\\' ? 2 : 1;
            }

            return Math.Min(i, regex.Length - 1);
        }

        private static char Escape(TerminalNode literal)
        {
            var escape = SqlSyntax.Unquote(literal.Text);
            if (escape.Length != 1)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "The LIKE escape '{0}' can't be used because it isn't exactly one character.", escape), SqlErrorKind.Semantic, literal.Start, literal.Length);
            }

            return escape[0];
        }

        private static string ConvertWildcards(string pattern)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Regex.Escape(pattern)); // Turn "off" all regular expression related syntax in the pattern string

            /* The previous call to Regex.Escape actually turned off
            * too many metacharacters, i.e. those which are recognized by
            * both the regular expression engine and the SQL LIKE
            * statement ([...] and [^...]). Those metacharacters have
            * to be manually unescaped here. */
            builder.Replace(@"\[", "[").Replace(@"\]", "]").Replace(@"[\^", "[^");

            var escaped = builder.ToString();
            builder.Clear();
            for (var i = 0; i < escaped.Length; i++)
            {
                if (escaped[i] == '\\' && i + 1 < escaped.Length)
                {
                    builder.Append(escaped, i++, 2);
                }
                else if (escaped[i] == '[' && escaped[ClassEnd(escaped, i)] == ']')
                {
                    var end = ClassEnd(escaped, i);
                    builder.Append(escaped, i, end - i + 1);
                    i = end;
                }
                else
                {
                    builder.Append(escaped[i] == '%' ? ".*" : escaped[i] == '_' ? "." : escaped[i].ToString());
                }
            }

            return builder.ToString();
        }
    }
}
