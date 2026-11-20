namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_STDEVP_LPAREN_RPAREN)]
    public class StandardDeviationPNode : VarianceNode
    {
        public StandardDeviationPNode()
        {
            Name = "StandardDeviationP";
        }

        internal override bool Sample
        {
            get
            {
                return false;
            }
        }
    }
}
