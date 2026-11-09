using System;
using System.Collections.Generic;
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
            if (key == null)
            {
                var body = Expression.Call(
                    typeof(QuantifiedComparisonNode),
                    nameof(Compare),
                    null,
                    Expression.Convert(left, typeof(object)),
                    TupleSelectNode.CreateUntypedValues(Subquery, results),
                    Expression.Constant(Test()),
                    Expression.Constant(All));

                return TupleSelectNode.Guard(body);
            }

            var keys = QueryTree.Select(results, QueryTree.ElementOf(results.Type), key, row => ExpressionFactory.ToKey(row, key));
            var values = QueryTree.Materialize(Subquery, Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { key }, keys));
            var value = ExpressionFactory.ToKey(left, key);

            return QueryTree.Simple(left) ? Typed(values, value, key) : QueryTree.Bind(value, bound => Typed(values, bound, key));
        }

        private Expression Typed(Expression values, Expression value, Type key)
        {
            return QueryTree.Bind(values, list =>
            {
                var known = QueryTree.Where(list, key, v => Expression.NotEqual(v, Expression.Constant(null, key)));
                var candidate = Expression.Parameter(key, "candidate");
                var test = Expression.Lambda(Expression.GetFuncType(key, typeof(bool)), Test(value, candidate), candidate);
                var matches = Expression.Call(typeof(Enumerable), All ? nameof(Enumerable.All) : nameof(Enumerable.Any), new[] { key }, known, test);
                var satisfied = All
                    ? Expression.AndAlso(Expression.Not(Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { key }, list, Expression.Constant(null, key))), matches)
                    : (Expression)matches;

                return Expression.Condition(
                    Expression.Not(Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { key }, list)),
                    Expression.Constant(All),
                    Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, key)), satisfied));
            });
        }

        private Expression Test(Expression left, Expression right)
        {
            switch ((RuleConstants)Rule.Index)
            {
                case RuleConstants.RULE_PREDEXP_EQ_LPAREN_RPAREN:
                    return Expression.Equal(left, right);
                case RuleConstants.RULE_PREDEXP_LTGT_LPAREN_RPAREN:
                case RuleConstants.RULE_PREDEXP_EXCLAMEQ_LPAREN_RPAREN:
                    return Expression.NotEqual(left, right);
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

        private static bool Compare(object value, HashSet<object> values, Func<object, object, bool> test, bool all)
        {
            if (values.Count == 0)
            {
                return all;
            }

            if (value == null)
            {
                return false;
            }

            var key = SourcesExtensions.KeyOf(value);
            var known = values.Where(v => v != null);

            return all ? !values.Contains(null) && known.All(v => test(key, v)) : known.Any(v => test(key, v));
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

            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "{0} and {1} can't be compared.", left, right));
        }
    }
}
