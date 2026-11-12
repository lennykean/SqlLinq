using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

using GoldParser;

using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.SyntaxTree
{
    public class NonTerminalNode : SyntaxNode
    {
        private List<SyntaxNode> m_children = new List<SyntaxNode>();

        public NonTerminalNode()
        {
        }

        public string RuleName
        {
            get
            {
                return Rule.Name;
            }
        }

        internal Rule Rule { get; set; }

        public IReadOnlyList<SyntaxNode> Children => m_children;

        public override int Start => Terminals().FirstOrDefault(t => t.Start >= 0)?.Start ?? -1;

        public override int Length
        {
            get
            {
                var terminals = Terminals().Where(t => t.Start >= 0).ToList();

                return terminals.Count == 0 ? 0 : terminals[^1].Start + terminals[^1].Length - terminals[0].Start;
            }
        }

        public override string ToSql()
        {
            SqlException.ThrowIfNestedTooDeeply();

            return JoinSql(m_children.Select(c => c.ToSql()));
        }

        public NonTerminalNode Replace(Func<NonTerminalNode, NonTerminalNode> replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            var copy = Copy(node =>
            {
                var replaced = replacement(node);
                if (replaced == null || replaced == node)
                {
                    return replaced;
                }

                var placed = replaced.Copy();
                foreach (var terminal in placed.Terminals())
                {
                    terminal.Place(node.Start, node.Length);
                }

                return placed;
            });
            if (copy is ExpressionNode rewritten && this is ExpressionNode written)
            {
                rewritten.Source = written.Source ?? written.ToSql();
            }

            return copy;
        }

        public NonTerminalNode Bind(IEnumerable<ISqlFunction> functions)
        {
            if (functions == null)
            {
                throw new ArgumentNullException(nameof(functions));
            }

            var copy = Copy();
            copy.Bind(functions.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase));

            return copy;
        }

        private void Bind(Dictionary<string, ISqlFunction> functions)
        {
            foreach (var node in FindDescendants<NonTerminalNode>().Prepend(this))
            {
                if (node is NodeWithId call && call.Function != null)
                {
                    call.Function = functions.TryGetValue(call.FunctionName, out var function) ? function : call.Function;
                    Bind(call.Arguments, functions);
                }
                else if (node is ColumnSource column && column.Function != null)
                {
                    column.Function = functions.TryGetValue(column.FunctionName, out var function) ? function : column.Function;
                    Bind(column.Arguments, functions);
                }
            }
        }

        private static void Bind(IEnumerable<ArgumentNode> arguments, Dictionary<string, ISqlFunction> functions)
        {
            foreach (var argument in arguments.Where(a => a.Expression != null))
            {
                argument.Expression.Bind(functions);
            }
        }

        /// <summary>
        /// This method is used during parsing to enforce syntax rules not expressed in the grammar
        /// </summary>
        internal virtual void CheckSyntax()
        {
        }

        public override string ToString()
        {
            return ToXml().ToString();
        }

        public override XElement ToXml()
        {
            XElement element = new XElement(Rule.Name.Replace("<", "").Replace(">", "").Replace(" ", ""));

            element.Add(new XAttribute("type", GetType().Name));
            element.Add(new XAttribute("ruleId", (RuleConstants)Rule.Index));

            foreach (var n in m_children)
                element.Add(n.ToXml());

            return element;
        }

        internal protected string GetTerminalText(string name)
        {
            TerminalNode terminal = GetTerminal(name);
            return terminal != null ? terminal.Text : string.Empty;
        }

        internal TerminalNode GetTerminal(string name)
        {
            return m_children.OfType<TerminalNode>().SingleOrDefault(node => node.Symbol.Name == name);
        }

        internal IEnumerable<TerminalNode> Terminals()
        {
            var pending = new Stack<SyntaxNode>(m_children.AsEnumerable().Reverse());
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node is TerminalNode terminal)
                {
                    yield return terminal;
                }
                else
                {
                    foreach (var child in ((NonTerminalNode)node).m_children.AsEnumerable().Reverse())
                    {
                        pending.Push(child);
                    }
                }
            }
        }

        internal NonTerminalNode Copy()
        {
            return Copy(node => null);
        }

        internal NonTerminalNode Copy(Func<NonTerminalNode, NonTerminalNode> replacement)
        {
            SqlException.ThrowIfNestedTooDeeply();
            var replaced = replacement(this);
            if (replaced != null && replaced != this)
            {
                return replaced.Copy();
            }

            var copy = SyntaxRuleFactory.CreateNode(Rule);
            foreach (var child in m_children)
            {
                copy.AppendChildNode(child is NonTerminalNode node ? (SyntaxNode)node.Copy(replacement) : ((TerminalNode)child).Copy());
            }

            CopyTo(copy, replacement);

            return copy;
        }

        internal virtual void CopyTo(NonTerminalNode copy, Func<NonTerminalNode, NonTerminalNode> replacement)
        {
        }

        internal static string JoinSql(IEnumerable<string> parts)
        {
            var sql = new StringBuilder();
            var previous = string.Empty;
            foreach (var part in parts.Where(p => p.Length > 0))
            {
                var call = part == "(" && previous.Length > 0 && char.IsLetter(previous[previous.Length - 1]) && !SqlSyntax.Keywords.Contains(previous, StringComparer.OrdinalIgnoreCase);
                if (sql.Length > 0 && !previous.EndsWith('(') && part != ")" && part != "," && !call)
                {
                    sql.Append(' ');
                }

                sql.Append(part);
                previous = part;
            }

            return sql.ToString();
        }

        internal protected IEnumerable<T> FindDescendants<T>() where T : NonTerminalNode
        {
            SqlException.ThrowIfNestedTooDeeply();
            var list = new List<T>();
            foreach (NonTerminalNode node in m_children.OfType<NonTerminalNode>())
            {
                if (node is T)
                    list.Add(node as T);

                list.AddRange(node.FindDescendants<T>());
            }

            return list;
        }

        internal T FindChild<T>(RuleConstants child) where T : NonTerminalNode
        {
            return m_children.OfType<T>().SingleOrDefault(node => node.Rule.Index == (int)child);
        }

        internal protected T FindChild<T>() where T : SyntaxNode
        {
            return m_children.OfType<T>().SingleOrDefault();
        }

        internal protected T FindChild<T>(int index) where T : SyntaxNode
        {
            if (index > m_children.Count)
                return null;

            return m_children[index] as T;
        }

        internal void AppendChildNode(SyntaxNode node)
        {
            Debug.Assert(node != null);

            m_children.Add(node);
            node.Index = m_children.IndexOf(node);
            node.Parent = this;
        }
    }
}
