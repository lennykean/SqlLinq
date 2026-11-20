namespace SqlLinq.SyntaxTree.Aggregates
{
    [SyntaxNode(RuleConstants.RULE_AGGREGATE_STDEV_LPAREN_RPAREN)]
    public class StandardDeviationNode : VarianceNode
    {
        public StandardDeviationNode()
        {
            Name = "StandardDeviation";
        }
    }
}
