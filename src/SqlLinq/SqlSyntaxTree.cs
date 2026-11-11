using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Aggregates;
using SqlLinq.SyntaxTree.Clauses;
using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.SyntaxTree.Joins;

namespace SqlLinq
{
    public sealed class SqlSyntaxTree
    {
        private SqlSyntaxTree(string text, NonTerminalNode root, SqlException error, IReadOnlyList<Identifier> names)
        {
            Text = text;
            Root = root;
            Error = error;
            Names = names;
        }

        public string Text { get; }

        public NonTerminalNode Root { get; }

        public SqlException Error { get; }

        public IReadOnlyList<Identifier> Names { get; }

        public static SqlSyntaxTree Parse(string sql)
        {
            return Parse(sql, null);
        }

        public static SqlSyntaxTree Parse(string sql, IEnumerable<ISqlFunction> functions)
        {
            var parser = new SqlParser(functions, 0, true);
            var parsed = Read(() => sql?.Length == 0 ? parser.Parse(new StringReader(sql)) : parser.Parse(sql), out var error);
            error ??= parsed ? parser.ArgumentError : new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin statement\n\t{1}", parser.ErrorString, parser.ErrorLine), parser.ErrorStart, parser.ErrorLength);

            return Build(sql, parsed ? parser.SyntaxTree : null, error, parser);
        }

        public static SqlSyntaxTree ParseExpression(string expression)
        {
            return ParseExpression(expression, null);
        }

        public static SqlSyntaxTree ParseExpression(string expression, IEnumerable<ISqlFunction> functions)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return ParseExpression(expression, 0, expression.Length, functions);
        }

        public static SqlSyntaxTree ParseExpression(string text, int start, int length, IEnumerable<ISqlFunction> functions)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var expression = text.Substring(start, length);
            var parser = new SqlParser(functions, start, true);
            var parsed = Read(() => parser.ParseExpression(expression), out var error);
            error ??= parsed ? parser.ArgumentError : new SqlException(string.Format(CultureInfo.CurrentCulture, "SQL parse error:\n\t{0}\nin expression\n\t{1}", parser.ErrorString, expression), parser.ErrorStart, parser.ErrorLength);

            return Build(text, parsed ? parser.Expression : null, error, parser);
        }

        public SqlSyntaxTree Replace(Func<NonTerminalNode, NonTerminalNode> replacement)
        {
            if (Root == null)
            {
                throw new InvalidOperationException("The text didn't parse, so it has no tree to rewrite.");
            }

            var root = Root.Replace(replacement);

            return new SqlSyntaxTree(Text, root, Error, Members(root).Select(t => SqlSyntax.Name(t)).ToList());
        }

        public SqlPlan<TResult> Compile<TSource, TResult>()
        {
            return Compile<TSource, TResult>(false);
        }

        public SqlPlan<TResult> Compile<TResult>(IReadOnlyDictionary<string, Type> tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            return SourcesExtensions.Compile<object, TResult>(this, tables, false, true);
        }

        internal SqlPlan<TResult> Compile<TSource, TResult>(bool bare)
        {
            return SourcesExtensions.Compile<TSource, TResult>(this, new Dictionary<string, Type> { ["this"] = typeof(TSource) }, false, false, bare);
        }

        private static bool Read(Func<bool> parse, out SqlException error)
        {
            error = null;
            try
            {
                return parse();
            }
            catch (SqlException e)
            {
                error = e;

                return false;
            }
        }

        private static SqlSyntaxTree Build(string text, NonTerminalNode root, SqlException error, SqlParser parser)
        {
            var roots = root != null ? new[] { root } : parser.Fragments.ToArray();
            try
            {
                var names = roots.SelectMany(Members).OrderBy(t => t.Start).Select(t => SqlSyntax.Name(t)).ToList();
                Locate(text, roots);

                return new SqlSyntaxTree(text, root, error, names);
            }
            catch (SqlException e)
            {
                return new SqlSyntaxTree(text, root, error ?? e, Array.Empty<Identifier>());
            }
        }

        private static void Locate(string text, IEnumerable<NonTerminalNode> roots)
        {
            var lines = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines.Add(i + 1);
                }
            }

            foreach (var terminal in roots.SelectMany(Terminals).Where(t => t.Start >= 0 && t.Start <= text.Length))
            {
                var line = lines.BinarySearch(terminal.Start);
                line = line < 0 ? ~line - 1 : line;
                terminal.Locate(line + 1, terminal.Start - lines[line] + 1);
            }
        }

        private static IEnumerable<TerminalNode> Terminals(NonTerminalNode node)
        {
            var arguments = node.FindDescendants<NonTerminalNode>().Prepend(node).SelectMany(Arguments).Where(a => a.Expression != null);

            return node.Terminals().Concat(arguments.SelectMany(a => Terminals(a.Expression)));
        }

        private static IEnumerable<ArgumentNode> Arguments(NonTerminalNode node)
        {
            return (node is NodeWithId call ? call.Arguments : (node as ColumnSource)?.Arguments) ?? Enumerable.Empty<ArgumentNode>();
        }

        private static IEnumerable<TerminalNode> Members(NonTerminalNode root)
        {
            foreach (var node in root.FindDescendants<NonTerminalNode>().Prepend(root))
            {
                var call = node is NodeWithId value ? value.Function : (node as ColumnSource)?.Function;
                if (call != null)
                {
                    foreach (var member in Arguments(node).Where(a => a.Expression != null).SelectMany(a => Members(a.Expression)))
                    {
                        yield return member;
                    }
                }
                else if (node is Join)
                {
                    foreach (var id in Ids(node))
                    {
                        yield return id;
                    }
                }
                else if (IsMember(node))
                {
                    yield return Ids(node).First();
                }
            }
        }

        private static IEnumerable<TerminalNode> Ids(NonTerminalNode node)
        {
            return node.Children.OfType<TerminalNode>().Where(t => t.Symbol.Name == "Id");
        }

        private static bool IsMember(NonTerminalNode node)
        {
            switch ((RuleConstants)node.Rule.Index)
            {
                case RuleConstants.RULE_VALUE_ID:
                    var id = ((NodeWithId)node).Id;
                    var select = node.EnclosingSelect();
                    var having = select?.HavingClause;

                    return having == null || id.Qualifier.Length > 0 || !node.AncestorsBelow(select).Contains(having) || !select.Columns.ColumnSources.Any(c => IsAlias(c, id.LookupId));

                case RuleConstants.RULE_IDMEMBER_ID:
                    return node.AncestorsBelow(null).OfType<GroupByClause>().Any();

                case RuleConstants.RULE_COLUMNSOURCE_ID:
                case RuleConstants.RULE_COLUMNSOURCE_ID2:
                    return true;

                case RuleConstants.RULE_ORDERLIST_ID:
                case RuleConstants.RULE_ORDERLIST_ID_COMMA:
                    var item = (OrderByItem)node;
                    var column = item.EnclosingSelect() == null ? null : item.SelectColumn();

                    return column == null || !IsAlias(column, item.Id.LookupId);

                default:
                    return false;
            }
        }

        private static bool IsAlias(ColumnSource column, string name)
        {
            return string.Equals(column.ColumnAlias, name, StringComparison.OrdinalIgnoreCase)
                && (column is AggregateNode || column is ComputedColumn || !string.Equals(column.Id.LocalId, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
