using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;

using GoldParser;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Aggregates;
using SqlLinq.SyntaxTree.Clauses;
using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.SyntaxTree.Joins;

namespace SqlLinq
{
    public class SqlParser
    {
        internal const string Placeholder = "$sqlfn";

        private const string HavingPrefix = "SELECT * FROM this HAVING ";

        private readonly Dictionary<string, ISqlFunction> _functions;

        private readonly List<TerminalNode> _terminals = new List<TerminalNode>();

        private readonly List<NonTerminalNode> _reductions = new List<NonTerminalNode>();

        private readonly bool _lenient;

        private int _offset;

        private string _source;

        private bool _expression;

        private bool _aggregates;

        private Parser m_parser;

        public SqlParser() : this(null)
        {
        }

        internal SqlParser(IEnumerable<ISqlFunction> functions)
        {
            _functions = ByName(functions ?? Enumerable.Empty<ISqlFunction>());

            foreach (var function in MathFunction.All)
            {
                _functions.TryAdd(function.Name, function);
            }
        }

        internal SqlParser(IEnumerable<ISqlFunction> functions, int offset, bool lenient)
            : this(functions)
        {
            _offset = offset;
            _lenient = lenient;
        }

        public string ErrorString { get; private set; }

        internal int ErrorStart { get; private set; } = -1;

        internal int ErrorLength { get; private set; }

        internal SqlErrorKind ErrorKind { get; private set; }

        internal SqlException ArgumentError { get; private set; }

        internal IEnumerable<NonTerminalNode> Fragments => _reductions.Where(n => n.Parent == null);

        public NonTerminalNode SyntaxTree { get; private set; }

        internal ExpressionNode Expression { get; private set; }

        public string ErrorLine
        {
            get
            {
                if (_source == null || ErrorStart < 0)
                {
                    return m_parser != null ? m_parser.LineText : "";
                }

                var end = Math.Min(ErrorStart - _offset + ErrorLength, _source.Length);
                var start = _source.LastIndexOf('\n', Math.Max(end - 1, 0)) + 1;

                return _source[start..end];
            }
        }

        public int LinePosition
        {
            get
            {
                return m_parser != null ? m_parser.LinePosition : 0;
            }
        }

        public int LineNumber
        {
            get
            {
                return m_parser != null ? m_parser.LineNumber : 0;
            }
        }

        public bool Parse(string source)
        {
            Debug.Assert(string.IsNullOrEmpty(source) == false);

            List<FunctionCall> calls;
            try
            {
                calls = SqlSyntax.FindCalls(source, _functions.Keys);
            }
            catch (SqlException e) when (_offset != 0)
            {
                throw new SqlException(e.Message, e.InnerException, e.Kind, e.Start + _offset, e.Length);
            }

            var text = SqlSyntax.ReplaceCalls(source, calls, PlaceholderFor);

            ArgumentError = null;

            using (TextReader reader = new StringReader(text))
            {
                bool parsed;
                try
                {
                    parsed = Parse(reader, source);
                }
                catch (SqlException e) when (_reductions.Count > 0)
                {
                    Place(calls);
                    var failed = _reductions[_reductions.Count - 1];
                    var (start, length) = e.Start < 0 ? (failed.Start, failed.Length) : ToSource(calls, e.Start, e.Length);

                    throw new SqlException(e.Message, e.InnerException, e.Kind, start, length);
                }

                Place(calls);
                if (!parsed)
                {
                    return false;
                }
            }

            foreach (var node in SyntaxTree.FindDescendants<NodeWithId>())
            {
                if (node.Id.LookupId.StartsWith(Placeholder, StringComparison.Ordinal))
                {
                    var call = calls[int.Parse(node.Id.LookupId.Substring(Placeholder.Length), CultureInfo.InvariantCulture)];
                    node.Function = _functions[call.Name];
                    node.FunctionName = call.Name;
                    node.Arguments = Arguments(call, node);
                }
            }

            var columns = SyntaxTree.FindDescendants<ColumnSource>().Where(c => !(c is AggregateNode) && !(c is ComputedColumn));
            foreach (var column in columns.Where(c => c.Id.LookupId.StartsWith(Placeholder, StringComparison.Ordinal)))
            {
                var call = calls[int.Parse(column.Id.LookupId.Substring(Placeholder.Length), CultureInfo.InvariantCulture)];
                column.Function = _functions[call.Name];
                column.FunctionName = call.Name;
                column.Arguments = Arguments(call, column);
            }

            foreach (var select in SyntaxTree.FindDescendants<SelectNode>().Prepend(SyntaxTree).OfType<SelectNode>())
            {
                try
                {
                    select.CheckSyntax();
                }
                catch (SqlException e) when (e.Start < 0)
                {
                    throw new SqlException(e.Message, e.InnerException, e.Kind, select.Start, select.Length);
                }
            }

            var ordered = SyntaxTree.FindDescendants<OrderByItem>().FirstOrDefault(o => o.Id.LookupId.StartsWith(Placeholder, StringComparison.Ordinal));
            var joined = SyntaxTree.FindDescendants<Join>().SelectMany(j => j.Children.OfType<TerminalNode>()).Select(t => SqlSyntax.Name(t)).FirstOrDefault(i => i.LookupId.StartsWith(Placeholder, StringComparison.Ordinal));
            var misplaced = joined ?? ordered?.Id;
            if (misplaced != null)
            {
                var error = new SqlException(string.Format(CultureInfo.CurrentCulture, joined != null ? "JOIN ON can't call {0} because it compares column names, not function calls." : "ORDER BY can't call {0} because it orders by column names, not function calls.", source.Substring(misplaced.Start - _offset, misplaced.Length)), SqlErrorKind.Semantic, misplaced.Start, misplaced.Length);
                if (!_lenient)
                {
                    throw error;
                }

                ArgumentError ??= error;
            }

            return true;
        }

        internal bool ParseExpression(string expression)
        {
            _expression = true;
            _offset -= HavingPrefix.Length;
            try
            {
                if (!Parse(HavingPrefix + expression))
                {
                    return false;
                }
            }
            finally
            {
                _offset += HavingPrefix.Length;
            }

            var having = ((SelectNode)SyntaxTree).HavingClause;
            var aggregate = having.FindDescendants<AggregateNode>().FirstOrDefault(a => !a.AncestorsBelow(having).OfType<SelectNode>().Any());
            if (aggregate != null && !_aggregates)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} can't be used here because it's an aggregate.", aggregate.ToSql()), SqlErrorKind.Semantic, aggregate.Start, aggregate.Length);
            }

            Expression = having.FindChild<ExpressionNode>();
            if (Expression == null)
            {
                Expression = (ExpressionNode)SyntaxRuleFactory.CreateNode(new Rule((int)RuleConstants.RULE_VALUE2, new Symbol((int)SymbolConstants.SYMBOL_VALUE, "Value", SymbolType.NonTerminal), new[] { aggregate.Rule.NonTerminal }));
                Expression.AppendChildNode(aggregate);
            }

            Expression.Parent = null;
            Expression.Source = expression;

            return true;
        }

        private static string PlaceholderFor(int index)
        {
            return "[" + Placeholder + index.ToString(CultureInfo.InvariantCulture) + "]";
        }

        private void Place(List<FunctionCall> calls)
        {
            foreach (var terminal in _terminals)
            {
                var (start, length) = ToSource(calls, terminal.Start, terminal.Length);
                terminal.Place(start, length);
            }

            if (ErrorStart >= 0)
            {
                var (end, size) = ToSource(calls, ErrorStart + ErrorLength - 1, 1);
                var (start, length) = ToSource(calls, ErrorStart, ErrorLength);
                (ErrorStart, ErrorLength) = (start, ErrorLength > 0 ? end + size - start : length);
            }
        }

        private (int Start, int Length) ToSource(List<FunctionCall> calls, int start, int length)
        {
            var shift = 0;
            for (var i = 0; i < calls.Count && start >= calls[i].Start + shift; i++)
            {
                var call = calls[i].End - calls[i].Start + 1;
                var placeholder = PlaceholderFor(i).Length;
                if (start < calls[i].Start + shift + placeholder)
                {
                    return (calls[i].Start + _offset, call);
                }

                shift += placeholder - call;
            }

            return (start - shift + _offset, length);
        }

        private List<ArgumentNode> Arguments(FunctionCall call, NonTerminalNode node)
        {
            var arguments = new List<ArgumentNode>();
            if (call.Args.Trim().Length == 0)
            {
                return arguments;
            }

            var at = 0;
            foreach (var text in SqlSyntax.SplitTopLevel(call.Args))
            {
                at = call.Args.IndexOf(text, at, StringComparison.Ordinal);
                var start = _offset + call.End - call.Args.Length + at;
                var parser = new SqlParser(_functions.Values, start, _lenient) { _aggregates = (!_expression || _aggregates) && node.AncestorsBelow(node.EnclosingSelect()).Any(a => a is Columns || a is HavingClause) };
                ExpressionNode expression = null;
                SqlException error;
                try
                {
                    error = parser.ParseExpression(text)
                        ? parser.ArgumentError
                        : new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin argument\n\t{1}\nof {2}()", parser.ErrorString, text.Trim(), call.Name), parser.ErrorKind, parser.ErrorStart, parser.ErrorLength);
                }
                catch (SqlException e) when (_lenient)
                {
                    error = e;
                }

                if (error != null && !_lenient)
                {
                    throw error;
                }

                if (parser.Expression != null && (error == null || error == parser.ArgumentError))
                {
                    expression = parser.Expression;
                    expression.Parent = node;
                }

                ArgumentError ??= error;
                arguments.Add(new ArgumentNode(text.Trim(), expression, error, start, text.Length) { Parent = node });
                at += text.Length;
            }

            return arguments;
        }

        internal static Dictionary<string, ISqlFunction> ByName(IEnumerable<ISqlFunction> functions)
        {
            var named = new Dictionary<string, ISqlFunction>(StringComparer.OrdinalIgnoreCase);
            foreach (var function in functions)
            {
                if (!named.TryAdd(function.Name, function))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "The functions can't be used because more than one is named '{0}'.", function.Name), nameof(functions));
                }
            }

            return named;
        }

        public bool Parse(TextReader sourceReader)
        {
            return Parse(sourceReader, null);
        }

        private bool Parse(TextReader sourceReader, string source)
        {
            Debug.Assert(sourceReader != null);

            _source = source;
            m_parser = ParserFactory.CreateParser(sourceReader);
            m_parser.TrimReductions = true;
            ErrorStart = -1;
            ErrorLength = 0;
            _terminals.Clear();
            _reductions.Clear();
            var comment = -1;

            while (true)
            {
                switch (m_parser.Parse())
                {
                    case ParseMessage.LexicalError:
                        ErrorString = string.Format(CultureInfo.CurrentCulture, "Lexical Error. Line {0}. Token {1} was not expected.", m_parser.LineNumber, m_parser.TokenText);
                        ErrorKind = m_parser.TokenText.StartsWith('\'') ? SqlErrorKind.UnterminatedString : SqlErrorKind.Lexical;
                        ErrorStart = m_parser.TokenCharPosition;
                        ErrorLength = m_parser.TokenLength;
                        return false;

                    case ParseMessage.SyntaxError:
                        StringBuilder text = new StringBuilder();
                        foreach (Symbol tokenSymbol in m_parser.GetExpectedTokens())
                            text.AppendFormat(CultureInfo.CurrentCulture, " {0}", tokenSymbol);

                        var opened = new Stack<TerminalNode>();
                        foreach (var read in _terminals.Take(_terminals.Count - 1))
                        {
                            if (read.Text == "(")
                            {
                                opened.Push(read);
                            }
                            else if (read.Text == ")" && opened.Count > 0)
                            {
                                opened.Pop();
                            }
                        }

                        var call = m_parser.TokenText == "(" && _terminals.Count > 1 && _terminals[^2].Symbol.Name == "Id" ? _terminals[^2] : null;
                        var unbalanced = m_parser.TokenSymbol.SymbolType == SymbolType.End && opened.Count > 0 ? opened.Peek() : m_parser.TokenText == ")" && opened.Count == 0 ? _terminals[^1] : null;
                        ErrorString = string.Format(CultureInfo.CurrentCulture, "Syntax Error. Line {0}. Expecting: {1}.", m_parser.LineNumber, text);
                        ErrorKind = call != null ? SqlErrorKind.UnknownName : unbalanced != null ? SqlErrorKind.UnbalancedParentheses : SqlErrorKind.Syntax;
                        ErrorStart = call?.Start ?? unbalanced?.Start ?? m_parser.TokenCharPosition;
                        ErrorLength = call?.Length ?? (unbalanced == null ? m_parser.TokenLength : m_parser.TokenCharPosition + m_parser.TokenLength - unbalanced.Start);

                        return false;

                    case ParseMessage.Reduction:
                        NonTerminalNode nonTerminal = SyntaxRuleFactory.CreateNode(m_parser.ReductionRule);
                        m_parser.TokenSyntaxNode = nonTerminal;
                        _reductions.Add(nonTerminal);

                        for (int i = 0; i < m_parser.ReductionCount; i++)
                            nonTerminal.AppendChildNode(m_parser.GetReductionSyntaxNode(i) as SyntaxNode);

                        // post parsing syntax check (used to do things like segregate the difference between HAVING and WHERE 
                        // expressions in terms of the validtity of aggregate expressions)
                        nonTerminal.CheckSyntax();

                        break;

                    case ParseMessage.TokenRead:
                        var terminal = new TerminalNode(m_parser);
                        _terminals.Add(terminal);
                        m_parser.TokenSyntaxNode = terminal;
                        break;

                    case ParseMessage.Accept:
                        SyntaxTree = m_parser.TokenSyntaxNode as NonTerminalNode;
                        ErrorString = null;
                        return true;

                    case ParseMessage.InternalError:
                        ErrorString = "Internal Error. Something is horribly wrong.";
                        return false;

                    case ParseMessage.NotLoadedError:
                        ErrorString = "Grammar Table is not loaded.";
                        return false;

                    case ParseMessage.CommentError:
                        ErrorString = "Comment Error. Unexpected end of input.";
                        ErrorKind = SqlErrorKind.UnterminatedComment;
                        ErrorStart = comment;
                        ErrorLength = m_parser.TokenCharPosition - comment;
                        return false;

                    case ParseMessage.CommentBlockRead:
                        comment = m_parser.TokenCharPosition;
                        break;

                    case ParseMessage.CommentLineRead:
                        // don't do anything 
                        break;
                }
            }
        }
    }
}
