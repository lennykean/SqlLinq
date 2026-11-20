using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.SyntaxTree.Expressions.Arithmetic;
using SqlLinq.SyntaxTree.Expressions.Functions;
using SqlLinq.SyntaxTree.Expressions.Predicates.Comparison;

namespace SqlLinq.SyntaxTree.Aggregates
{
    /// <summary>
    /// Base class for other aggregate nodes
    /// </summary>
    /// <remarks>This is one case where using the parser's TrimReductions feature
    /// makes this code more complicated. TrimReductions will remove nodes from the tree that
    /// are not directly expressed in the input even though they are yielded by the state tables.
    /// For a statement like "SELECT AVG(field) FROM source" no Column Source node ends up in the tree.
    /// However for "SELECT field1, AVG(field2)" or "SELECT AVG(field) AS alias" the aggregate
    /// node will be the child of a column source node. So in order to deal with this difference in the resulting tree
    /// AggregateNode inherits from ColumnSource and can also be the child of ColumnSource</remarks>
    public abstract class AggregateNode : ColumnSource
    {
        private static readonly HashSet<int> DistinctRules = new HashSet<int>
        {
            (int)RuleConstants.RULE_AGGREGATE_AVG_LPAREN_DISTINCT_RPAREN,
            (int)RuleConstants.RULE_AGGREGATE_MIN_LPAREN_DISTINCT_RPAREN,
            (int)RuleConstants.RULE_AGGREGATE_MAX_LPAREN_DISTINCT_RPAREN,
            (int)RuleConstants.RULE_AGGREGATE_SUM_LPAREN_DISTINCT_RPAREN,
        };

        protected AggregateNode()
        {
        }

        public string Name { get; protected set; }

        private bool Distinct
        {
            get
            {
                return DistinctRules.Contains(Rule.Index);
            }
        }

        public override string ColumnAlias
        {
            get
            {
                if (Parent is ColumnSource)
                    return ((ColumnSource)Parent).ColumnAlias;

                return base.ColumnAlias;
            }
        }

        protected override Identifier CreateIdentifier()
        {
            var name = Distinct ? Name + "Distinct" : Name;

            if (DoNotDereferenceFields)
                return new Identifier(name + "Value");

            return new Identifier(name + SourceFieldName);
        }

        protected string SourceFieldName
        {
            get
            {
                NodeWithId id = FindChild<NodeWithId>();
                return id == null || id.Function != null ? "Value" : id.Id.LocalId;
            }
        }
        public virtual bool DoNotDereferenceFields
        {
            get
            {
                return FindChild<Value>() != null;
            }
        }

        internal override IEnumerable<Identifier> GetFields()
        {
            return FindDescendants<NodeWithId>().Select(n => n.Id);
        }

        internal virtual Expression GetCallExpression(Type tSource, Expression param)
        {
            // if the target of the aggrgegate is itself a function
            if (DoNotDereferenceFields && !NullWhenEmpty)
            {
                // get the method that calculates the aggregate from the source collection
                MethodInfo method = GetEvaluationMethod(param.Type);
                if (method.IsGenericMethodDefinition)
                    method = method.MakeGenericMethod(tSource);

                var values = Distinct ? Expression.Call(typeof(Enumerable), nameof(Enumerable.Distinct), new[] { tSource }, param) : param;

                return Expression.Call(method, values);
            }

            var aggregate = GetPropertyOrFieldAggregateExpression(tSource, param);

            return aggregate.Object == null ? Complete(aggregate.Arguments[0], aggregate) : aggregate;
        }

        protected virtual MethodCallExpression GetPropertyOrFieldAggregateExpression(Type tSource, Expression param)
        {
            var values = DoNotDereferenceFields ? param : Selected(tSource, param);
            var type = values.Type.GetGenericArguments()[0];
            if (Distinct)
            {
                values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Distinct), new[] { type }, values);
            }

            if (NullWhenEmpty && type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                var value = Expression.Parameter(type, "value");
                var nullable = typeof(Nullable<>).MakeGenericType(type);
                values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), new[] { type, nullable }, values, Expression.Lambda(Expression.Convert(value, nullable), value));
            }

            var method = EvaluatatorType.GetMethod(Name, new[] { values.Type });
            if (method != null)
            {
                return Expression.Call(method, values);
            }

            var ordered = Ordered(values, type);
            if (ordered != null)
            {
                return ordered;
            }

            var element = values.Type.GetGenericArguments()[0];
            var underlying = Nullable.GetUnderlyingType(element) ?? element;
            var code = Type.GetTypeCode(underlying);
            if (element != typeof(object))
            {
                if (underlying.IsEnum || code < TypeCode.SByte || code > TypeCode.Decimal)
                {
                    throw new SqlException("Arithmetic can't use " + underlying.Name + " values because they aren't numbers.", SqlErrorKind.Semantic, Start, Length);
                }

                var promoted = code <= TypeCode.UInt16 ? typeof(int) : code <= TypeCode.UInt64 ? typeof(long) : null;
                var wide = promoted == null || element == underlying ? promoted : typeof(Nullable<>).MakeGenericType(promoted);
                method = wide == null ? null : EvaluatatorType.GetMethod(Name, new[] { typeof(IEnumerable<>).MakeGenericType(wide) });
                if (method != null)
                {
                    var value = Expression.Parameter(element, "value");
                    var widened = Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), new[] { element, wide }, values, Expression.Lambda(Expression.ConvertChecked(value, wide), value));

                    return Expression.Call(method, widened);
                }
            }

            var boxed = Expression.Call(typeof(Enumerable), nameof(Enumerable.Cast), new[] { typeof(object) }, values);

            return Expression.Call(Expression.Constant(this), typeof(AggregateNode).GetMethod(nameof(AggregateValues), BindingFlags.Instance | BindingFlags.NonPublic), boxed);
        }

        internal virtual bool NullWhenEmpty
        {
            get
            {
                return false;
            }
        }

        internal virtual Expression Complete(Expression values, MethodCallExpression aggregate)
        {
            return aggregate;
        }

        internal virtual MethodCallExpression Ordered(Expression values, Type type)
        {
            return null;
        }

        internal static MethodCallExpression Pick(Expression values, Type type, string order, string extreme)
        {
            if (type == typeof(object))
            {
                return null;
            }

            if (type != typeof(string))
            {
                return Expression.Call(typeof(Enumerable), extreme, new[] { values.Type.GetGenericArguments()[0] }, values);
            }

            var value = Expression.Parameter(type, "value");
            var present = Expression.Call(typeof(Enumerable), nameof(Enumerable.Where), new[] { type }, values, Expression.Lambda(Expression.NotEqual(value, Expression.Constant(null, type)), value));
            var sorted = Expression.Call(typeof(Enumerable), order, new[] { type, type }, present, Expression.Lambda(value, value), Expression.Constant(StringComparer.Ordinal, typeof(IComparer<string>)));

            return Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { type }, sorted);
        }

        internal static Expression Known(Expression values, Func<Expression, Expression> count)
        {
            var type = values.Type.GetGenericArguments()[0];
            var value = Expression.Parameter(type, "value");

            return count(Expression.Lambda(Expression.NotEqual(value, Expression.Constant(null, type)), value));
        }

        private MethodCallExpression Selected(Type tSource, Expression rows)
        {
            var selector = CreateSelector(tSource, rows);

            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), new[] { tSource, selector.ReturnType }, rows, selector);
        }

        internal LambdaExpression CreateSelector(Type tSource, Expression rows)
        {
            var row = Expression.Parameter(tSource, "row");

            return Expression.Lambda(Children.OfType<ExpressionNode>().First().CreateExpression((ParameterExpression)rows, row), row);
        }

        internal virtual object AggregateValues(IEnumerable<object> values)
        {
            var numbers = values.Where(v => v != null).Select(v => ArithmeticExpressionNode.ToNumber(v).Value).ToList();
            if (numbers.Count == 0)
            {
                return null;
            }

            var method = EvaluatatorType.GetMethod(Name, new[] { typeof(IEnumerable<double>) });

            return ((Func<IEnumerable<double>, double>)method.CreateDelegate(typeof(Func<IEnumerable<double>, double>)))(numbers);
        }

        internal static object Extreme(IEnumerable<object> values, int direction)
        {
            object best = null;
            foreach (var value in values.Where(v => v != null))
            {
                if (best == null || direction * QuantifiedComparisonNode.Order(SourcesExtensions.KeyOf(value), SourcesExtensions.KeyOf(best)) > 0)
                {
                    best = value;
                }
            }

            return best;
        }

        protected virtual Type EvaluatatorType
        {
            get
            {
                return typeof(Enumerable);
            }
        }

        protected virtual MethodInfo GetEvaluationMethod(Type paramType)
        {
            return EvaluatatorType.GetMethod(Name, new Type[] { paramType });
        }
    }
}
