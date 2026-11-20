using System.Collections.Generic;
using System.Linq;

using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.SyntaxTree.Joins;

namespace SqlLinq.SyntaxTree.Clauses
{
    [SyntaxNode(RuleConstants.RULE_FROMCLAUSE_FROM)]
    public class FromClause : NonTerminalNode
    {
        public FromClause()
        {
        }

        public string TableName
        {
            get
            {
                return GetTerminalText("Id");
            }
        }

        public Join Join
        {
            get
            {
                return FindDescendants<Join>().FirstOrDefault();
            }
        }

        internal List<(string Name, string Alias, SyntaxNode Node)> Members
        {
            get
            {
                return Of(Children.ElementAtOrDefault(1));
            }
        }

        internal static List<(string Name, string Alias, SyntaxNode Node)> Of(SyntaxNode node)
        {
            var members = new List<(string Name, string Alias, SyntaxNode Node)>();

            if (node is NodeWithId id)
            {
                members.Add((SqlSyntax.Unbracket(id.Id.LookupId), null, id));
            }
            else if (node is NonTerminalNode pair && (pair.Rule.Index == (int)RuleConstants.RULE_IDMEMBER_ID_ID || pair.Rule.Index == (int)RuleConstants.RULE_IDMEMBER_ID_AS_ID))
            {
                var ids = pair.Children.OfType<TerminalNode>().Where(t => t.Symbol.Name == "Id").Select(t => SqlSyntax.Unbracket(t.Text)).ToList();
                members.Add((ids[0], ids[1], pair));
            }
            else if (node is NonTerminalNode list)
            {
                foreach (var child in list.Children.OfType<NonTerminalNode>())
                {
                    members.AddRange(Of(child));
                }
            }

            return members;
        }
    }
}