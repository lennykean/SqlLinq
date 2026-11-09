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

        private readonly Dictionary<string, ISqlFunction> _functions;

        private Parser m_parser;

        public SqlParser()
        {
            _functions = new Dictionary<string, ISqlFunction>(StringComparer.OrdinalIgnoreCase);
        }

        internal SqlParser(IEnumerable<ISqlFunction> functions)
        {
            _functions = (functions ?? Enumerable.Empty<ISqlFunction>()).ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        public string ErrorString { get; private set; }

        public NonTerminalNode SyntaxTree { get; private set; }

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

            var calls = _functions.Count == 0 ? new List<FunctionCall>() : SqlSyntax.FindCalls(source, _functions.Keys);
            var text = SqlSyntax.ReplaceCalls(source, calls, i => "[" + Placeholder + i.ToString(CultureInfo.InvariantCulture) + "]");

            using (TextReader reader = new StringReader(text))
            {
                if (!Parse(reader))
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
                    node.Arguments = Arguments(call);
                    foreach (var argument in node.Arguments)
                    {
                        argument.Node.Parent = node;
                    }
                }
            }

            var columns = SyntaxTree.FindDescendants<ColumnSource>().Where(c => !(c is AggregateNode) && !(c is ComputedColumn));
            foreach (var column in columns.Where(c => c.Id.LookupId.StartsWith(Placeholder, StringComparison.Ordinal)))
            {
                var call = calls[int.Parse(column.Id.LookupId.Substring(Placeholder.Length), CultureInfo.InvariantCulture)];
                column.Function = _functions[call.Name];
                column.Arguments = Arguments(call);
                foreach (var argument in column.Arguments)
                {
                    argument.Node.Parent = column;
                }
            }

            return true;
        }

        private List<(string Text, ExpressionNode Node)> Arguments(FunctionCall call)
        {
            var arguments = new List<(string Text, ExpressionNode Node)>();
            if (call.Args.Trim().Length == 0)
            {
                return arguments;
            }

            foreach (var text in SqlSyntax.SplitTopLevel(call.Args))
            {
                var parser = new SqlParser(_functions.Values);
                if (!parser.Parse("SELECT * FROM this WHERE " + text))
                {
                    throw new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin argument\n\t{1}\nof {2}()", parser.ErrorString, text.Trim(), call.Name));
                }

                arguments.Add((text.Trim(), ((SelectNode)parser.SyntaxTree).WhereClause.FindChild<ExpressionNode>()));
            }

            return arguments;
        }

        public bool Parse(TextReader sourceReader)
        {
            Debug.Assert(sourceReader != null);

            m_parser = ParserFactory.CreateParser(sourceReader);
            m_parser.TrimReductions = true;

            while (true)
            {
                switch (m_parser.Parse())
                {
                    case ParseMessage.LexicalError:
                        ErrorString = string.Format(CultureInfo.CurrentCulture, "Lexical Error. Line {0}. Token {1} was not expected.", m_parser.LineNumber, m_parser.TokenText);
                        return false;

                    case ParseMessage.SyntaxError:
                        StringBuilder text = new StringBuilder();
                        foreach (Symbol tokenSymbol in m_parser.GetExpectedTokens())
                            text.AppendFormat(CultureInfo.CurrentCulture, " {0}", tokenSymbol);

                        ErrorString = string.Format(CultureInfo.CurrentCulture, "Syntax Error. Line {0}. Expecting: {1}.", m_parser.LineNumber, text);

                        return false;

                    case ParseMessage.Reduction:
                        NonTerminalNode nonTerminal = SyntaxRuleFactory.CreateNode(m_parser.ReductionRule);
                        m_parser.TokenSyntaxNode = nonTerminal;

                        for (int i = 0; i < m_parser.ReductionCount; i++)
                            nonTerminal.AppendChildNode(m_parser.GetReductionSyntaxNode(i) as SyntaxNode);

                        // post parsing syntax check (used to do things like segregate the difference between HAVING and WHERE 
                        // expressions in terms of the validtity of aggregate expressions)
                        nonTerminal.CheckSyntax();

                        break;

                    case ParseMessage.TokenRead:
                        m_parser.TokenSyntaxNode = new TerminalNode(m_parser);
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
                        return false;

                    case ParseMessage.CommentBlockRead:
                    case ParseMessage.CommentLineRead:
                        // don't do anything 
                        break;
                }
            }
        }
    }
}
