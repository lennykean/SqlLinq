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
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq
{
    public class SqlParser
    {
        private const string Placeholder = "$sqlfn";

        private const string WherePrefix = "SELECT * FROM this WHERE ";

        private readonly Dictionary<string, ISqlFunction> _functions;

        private readonly List<TerminalNode> _terminals = new List<TerminalNode>();

        private readonly List<NonTerminalNode> _reductions = new List<NonTerminalNode>();

        private readonly bool _lenient;

        private int _offset;

        private Parser m_parser;

        public SqlParser() : this(null)
        {
        }

        internal SqlParser(IEnumerable<ISqlFunction> functions)
        {
            _functions = (functions ?? Enumerable.Empty<ISqlFunction>())
                .ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

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

        internal SqlException ArgumentError { get; private set; }

        internal IEnumerable<NonTerminalNode> Fragments => _reductions.Where(n => n.Parent == null);

        public NonTerminalNode SyntaxTree { get; private set; }

        internal ExpressionNode Expression { get; private set; }

        public string ErrorLine
        {
            get
            {
                return m_parser != null ? m_parser.LineText : "";
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
                throw new SqlException(e.Message, e.InnerException, e.Start + _offset, e.Length);
            }

            var text = SqlSyntax.ReplaceCalls(source, calls, PlaceholderFor);

            ArgumentError = null;

            using (TextReader reader = new StringReader(text))
            {
                bool parsed;
                try
                {
                    parsed = Parse(reader);
                }
                catch (SqlException e) when (e.Start < 0 && _reductions.Count > 0)
                {
                    Place(calls);
                    var failed = _reductions[_reductions.Count - 1];

                    throw new SqlException(e.Message, e.InnerException, failed.Start, failed.Length);
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

            return true;
        }

        internal bool ParseExpression(string expression)
        {
            _offset -= WherePrefix.Length;
            try
            {
                if (!Parse(WherePrefix + expression))
                {
                    return false;
                }
            }
            finally
            {
                _offset += WherePrefix.Length;
            }

            Expression = ((SelectNode)SyntaxTree).WhereClause.FindChild<ExpressionNode>();
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
                (ErrorStart, ErrorLength) = ToSource(calls, ErrorStart, ErrorLength);
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
                var parser = new SqlParser(_functions.Values, start, _lenient);
                ExpressionNode expression = null;
                SqlException error;
                try
                {
                    error = parser.ParseExpression(text)
                        ? parser.ArgumentError
                        : new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin argument\n\t{1}\nof {2}()", parser.ErrorString, text.Trim(), call.Name), parser.ErrorStart, parser.ErrorLength);
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

        public bool Parse(TextReader sourceReader)
        {
            Debug.Assert(sourceReader != null);

            m_parser = ParserFactory.CreateParser(sourceReader);
            m_parser.TrimReductions = true;
            ErrorStart = -1;
            ErrorLength = 0;
            _terminals.Clear();
            _reductions.Clear();

            while (true)
            {
                switch (ParseNext())
                {
                    case ParseMessage.LexicalError:
                        ErrorString = string.Format(CultureInfo.CurrentCulture, "Lexical Error. Line {0}. Token {1} was not expected.", m_parser.LineNumber, m_parser.TokenText);
                        ErrorStart = m_parser.TokenCharPosition;
                        ErrorLength = m_parser.TokenLength;
                        return false;

                    case ParseMessage.SyntaxError:
                        StringBuilder text = new StringBuilder();
                        foreach (Symbol tokenSymbol in m_parser.GetExpectedTokens())
                            text.AppendFormat(CultureInfo.CurrentCulture, " {0}", tokenSymbol);

                        ErrorString = string.Format(CultureInfo.CurrentCulture, "Syntax Error. Line {0}. Expecting: {1}.", m_parser.LineNumber, text);
                        ErrorStart = m_parser.TokenCharPosition;
                        ErrorLength = m_parser.TokenLength;

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
                        ErrorStart = m_parser.TokenCharPosition;
                        ErrorLength = m_parser.TokenLength;
                        return false;

                    case ParseMessage.CommentBlockRead:
                    case ParseMessage.CommentLineRead:
                        // don't do anything 
                        break;
                }
            }
        }

        private ParseMessage ParseNext()
        {
            try
            {
                return m_parser.Parse();
            }
            catch (Exception e) when (e.Message == "CommentError")
            {
                return ParseMessage.CommentError;
            }
        }
    }
}
