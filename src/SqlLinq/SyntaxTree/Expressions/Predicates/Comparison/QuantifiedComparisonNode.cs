using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace SqlLinq.SyntaxTree.Expressions.Predicates.Comparison
{
    [SyntaxNode(RuleConstants.RULE_PREDEXP_EQ_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_LTGT_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_EXCLAMEQ_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_GT_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_GTEQ_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_LT_LPAREN_RPAREN)]
    [SyntaxNode(RuleConstants.RULE_PREDEXP_LTEQ_LPAREN_RPAREN)]
    internal sealed class QuantifiedComparisonNode : ExpressionNode
    {
        private NonTerminalNode Quantifier
        {
            get
            {
                return (NonTerminalNode)Children[2];
            }
        }

        private bool All
        {
            get
            {
                return Quantifier.Rule.Index == (int)RuleConstants.RULE_QUANTIFIER_ALL;
            }
        }

        private SelectNode Subquery
        {
            get
            {
                return Children.OfType<SelectNode>().Single();
            }
        }

        internal override void CheckSyntax()
        {
            TupleSelectNode.RequireOneColumn(Subquery, Quantifier.Children.OfType<TerminalNode>().Single().Text.ToUpperInvariant());
        }

        internal override Expression CreateExpression(ParameterExpression sourceData, ParameterExpression param)
        {
            var left = CreateChildExpression(sourceData, param, 0);
            var results = TupleSelectNode.CreateResults(Subquery, sourceData, param);
            var key = left.Type.KeyType(QueryTree.ElementOf(results.Type));
            if (key == null && left.Type != typeof(object) && QueryTree.ElementOf(results.Type) != typeof(object))
            {
                Compiled(() => Test(left, Expression.Parameter(QueryTree.ElementOf(results.Type))));
            }

            if (key == null)
            {
                var untyped = TupleSelectNode.CreateUntypedValues(Subquery, results);
                var boxed = Expression.Call(typeof(SourcesExtensions), nameof(SourcesExtensions.KeyOf), null, Expression.Convert(left, typeof(object)));

                return TupleSelectNode.Guard(QueryTree.Bind(boxed, bound => Quantified(untyped, bound, typeof(object))));
            }

            var keys = QueryTree.Select(results, QueryTree.ElementOf(results.Type), key, row => ExpressionFactory.ToKey(row, key));
            var values = QueryTree.Materialize(Subquery, Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { key }, keys));
            var value = ExpressionFactory.ToKey(left, key);

            return QueryTree.Simple(left) ? Quantified(values, value, key) : QueryTree.Bind(value, bound => Quantified(values, bound, key));
        }

        private MethodCallExpression Quantified(Expression values, Expression value, Type key)
        {
            var tests = QueryTree.Select(values, key, typeof(bool?), candidate =>
            {
                var compared = key == typeof(object) ? PredicateExpressionNode.Known(Expression.Invoke(Expression.Constant(Test()), value, candidate), value, candidate) : Test(value, candidate);

                return compared.Type == typeof(bool?) ? compared : Expression.Convert(compared, typeof(bool?));
            });
            var result = Expression.Parameter(typeof(bool?), "result");
            var test = Expression.Parameter(typeof(bool?), "test");

            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Aggregate), new[] { typeof(bool?), typeof(bool?) }, tests, Expression.Constant(All, typeof(bool?)), Expression.Lambda(All ? Expression.And(result, test) : Expression.Or(result, test), result, test));
        }

        private Expression Test(Expression left, Expression right)
        {
            switch ((RuleConstants)Rule.Index)
            {
                case RuleConstants.RULE_PREDEXP_EQ_LPAREN_RPAREN:
                    return PredicateExpressionNode.Known(Expression.Equal(left, right), left, right);
                case RuleConstants.RULE_PREDEXP_LTGT_LPAREN_RPAREN:
                case RuleConstants.RULE_PREDEXP_EXCLAMEQ_LPAREN_RPAREN:
                    return PredicateExpressionNode.Known(Expression.NotEqual(left, right), left, right);
                case RuleConstants.RULE_PREDEXP_GT_LPAREN_RPAREN:
                    return PredicateExpressionNode.Compare(left, right, Expression.GreaterThan);
                case RuleConstants.RULE_PREDEXP_GTEQ_LPAREN_RPAREN:
                    return PredicateExpressionNode.Compare(left, right, Expression.GreaterThanOrEqual);
                case RuleConstants.RULE_PREDEXP_LT_LPAREN_RPAREN:
                    return PredicateExpressionNode.Compare(left, right, Expression.LessThan);
                case RuleConstants.RULE_PREDEXP_LTEQ_LPAREN_RPAREN:
                default:
                    return PredicateExpressionNode.Compare(left, right, Expression.LessThanOrEqual);
            }
        }

        private Func<object, object, bool> Test()
        {
            switch ((RuleConstants)Rule.Index)
            {
                case RuleConstants.RULE_PREDEXP_EQ_LPAREN_RPAREN:
                    return (left, right) => Equals(left, right);
                case RuleConstants.RULE_PREDEXP_LTGT_LPAREN_RPAREN:
                case RuleConstants.RULE_PREDEXP_EXCLAMEQ_LPAREN_RPAREN:
                    return (left, right) => !Equals(left, right);
                case RuleConstants.RULE_PREDEXP_GT_LPAREN_RPAREN:
                    return (left, right) => Order(left, right) > 0;
                case RuleConstants.RULE_PREDEXP_GTEQ_LPAREN_RPAREN:
                    return (left, right) => Order(left, right) >= 0;
                case RuleConstants.RULE_PREDEXP_LT_LPAREN_RPAREN:
                    return (left, right) => Order(left, right) < 0;
                case RuleConstants.RULE_PREDEXP_LTEQ_LPAREN_RPAREN:
                default:
                    return (left, right) => Order(left, right) <= 0;
            }
        }

        internal static int Order(object left, object right)
        {
            if (left is string text && right is string other)
            {
                return string.CompareOrdinal(text, other);
            }

            if (left is IComparable comparable && left.GetType() == right.GetType())
            {
                return comparable.CompareTo(right);
            }

            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "{0} and {1} can't be compared without a common type.", left, right));
        }
    }
}
