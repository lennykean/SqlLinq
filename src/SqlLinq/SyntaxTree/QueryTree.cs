using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree.Clauses;

namespace SqlLinq.SyntaxTree
{
    internal static class QueryTree
    {
        internal static Expression Build(SelectNode select, Expression source, Type tSource, Type tResult)
        {
            SqlException.ThrowIfNestedTooDeeply();
            if (!(source is ParameterExpression parameter))
            {
                return Let(select, source, tSource, "source", rows => Over(select, rows, tSource, tResult));
            }

            return Scopes(select).Any(s => s.Parameter == parameter)
                ? Over(select, parameter, tSource, tResult)
                : Scoped(select, parameter, () => Over(select, parameter, tSource, tResult));
        }

        internal static Expression Materialize(SelectNode select, Expression value)
        {
            var scopes = Scopes(select);
            var target = scopes.Count == 0 ? -1 : 0;
            foreach (var parameter in FreeVariables.Of(value))
            {
                var index = scopes.FindIndex(s => s.Parameter == parameter || s.Materialized.Any(m => m.Value == parameter));
                if (index < 0)
                {
                    return value;
                }

                target = Math.Max(target, index);
            }

            if (target < 0)
            {
                return value;
            }

            var materialized = Expression.Parameter(value.Type, "materialized");
            scopes[target].Materialized.Add((materialized, value));

            return materialized;
        }

        private static Expression Over(SelectNode select, ParameterExpression source, Type tSource, Type tResult)
        {
            var columns = select.Columns;
            var having = select.HavingClause;
            var grouped = select.GroupByClause != null || columns.IsAggregateOnlyQuery;
            var havingOverRows = having != null && (grouped || having.HasAggregates);
            var ordered = select.OrderByClause != null && !(select.Parent is UnionNode);
            var sortsResults = ordered && select.OrderByClause.SortsResults;

            Expression filtered = source;
            if (select.WhereClause != null)
            {
                filtered = Where(filtered, tSource, row => select.WhereClause.CreatePredicate(source, row));
            }

            if (ordered && !sortsResults)
            {
                filtered = select.OrderByClause.Order(filtered, tSource);
            }

            var results = Let(select, filtered, tSource, "rows", rows => Selection(select, rows, tSource, tResult, havingOverRows ? having : null));
            var element = ElementOf(results.Type);
            if (having != null && !havingOverRows)
            {
                results = Let(select, results, element, "results", all => Where(all, element, result => having.CreatePredicate(all, result)));
            }

            if (columns.Distinct)
            {
                var comparer = tResult == null ? DefaultComparer(element) : DistinctComparer(tResult);
                results = Expression.Call(typeof(Enumerable), nameof(Enumerable.Distinct), new[] { element }, results, Expression.Constant(comparer, typeof(IEqualityComparer<>).MakeGenericType(element)));
            }

            return sortsResults ? select.OrderByClause.OrderResults(results, element) : results;
        }

        internal static bool Natural(SelectNode select)
        {
            var columns = select.Columns;
            var having = select.HavingClause;
            var grouped = select.GroupByClause != null || columns.IsAggregateOnlyQuery;

            return !(columns is ColumnsStar) && columns.ColumnSources.Count() == 1 && (having == null || grouped || having.HasAggregates);
        }

        internal static Expression Selection(SelectNode select, ParameterExpression rows, Type tSource, Type tResult, HavingClause having)
        {
            if (select.GroupByClause != null)
            {
                return select.GroupByClause.CreateGroups(rows, tSource, tResult, having);
            }

            if (having == null)
            {
                return select.Columns.CreateSelection(rows, tSource, tResult);
            }

            var sequence = Sequence(tSource);
            var whole = Where(
                Expression.Call(typeof(Enumerable), nameof(Enumerable.Repeat), new[] { sequence }, rows, Expression.Constant(1)),
                sequence,
                group => having.CreateGroupPredicate(group, tSource));

            return SelectMany(whole, sequence, tResult, group => select.Columns.CreateSelection(group, tSource, tResult));
        }

        internal static Expression Where(Expression source, Type element, Func<ParameterExpression, Expression> predicate)
        {
            var item = Expression.Parameter(element, "row");

            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Where),
                new[] { element },
                source,
                Expression.Lambda(Expression.GetFuncType(element, typeof(bool)), predicate(item), item));
        }

        internal static Expression Select(Expression source, Type element, Type result, Func<ParameterExpression, Expression> selector)
        {
            var item = Expression.Parameter(element, "row");
            var body = selector(item);
            var type = result ?? body.Type;

            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Select),
                new[] { element, type },
                source,
                Expression.Lambda(Expression.GetFuncType(element, type), body, item));
        }

        internal static Expression SelectMany(Expression source, Type element, Type result, Func<ParameterExpression, Expression> selector)
        {
            var item = Expression.Parameter(element, "rows");
            var body = selector(item);
            var type = result ?? ElementOf(body.Type);

            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.SelectMany),
                new[] { element, type },
                source,
                Expression.Lambda(Expression.GetFuncType(element, Sequence(type)), body, item));
        }

        internal static Expression Bind(Expression value, Func<ParameterExpression, Expression> body)
        {
            if (value is ParameterExpression parameter)
            {
                return body(parameter);
            }

            var bound = Expression.Parameter(value.Type, "value");
            var result = body(bound);
            var once = Expression.Call(typeof(Enumerable), nameof(Enumerable.Repeat), new[] { value.Type }, value, Expression.Constant(1));
            var selected = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Select),
                new[] { value.Type, result.Type },
                once,
                Expression.Lambda(Expression.GetFuncType(value.Type, result.Type), result, bound));

            return Expression.Call(typeof(Enumerable), nameof(Enumerable.First), new[] { result.Type }, selected);
        }

        internal static bool Simple(Expression value)
        {
            switch (value)
            {
                case ParameterExpression _:
                case ConstantExpression _:
                    return true;
                case MemberExpression member:
                    return member.Expression == null || Simple(member.Expression);
                case UnaryExpression unary when unary.NodeType == ExpressionType.Convert:
                    return Simple(unary.Operand);
                default:
                    return false;
            }
        }

        internal static Expression Let(SelectNode select, Expression value, Type element, string name, Func<ParameterExpression, Expression> body)
        {
            var sequence = Sequence(element);
            var parameter = Expression.Parameter(sequence, name);
            var result = Scoped(select, parameter, () => body(parameter));
            if (ParameterReplacer.Uses(result, parameter) <= 1)
            {
                return ParameterReplacer.Replace(result, parameter, value);
            }

            return SelectMany(Select(Unit(), typeof(int), sequence, _ => value), sequence, ElementOf(result.Type), _ => result, parameter);
        }

        internal static Type Sequence(Type element)
        {
            return typeof(IEnumerable<>).MakeGenericType(element);
        }

        internal static Type ElementOf(Type sequence)
        {
            return sequence.IsGenericType && sequence.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? sequence.GetGenericArguments()[0]
                : sequence.GetInterfaces().First(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GetGenericArguments()[0];
        }

        internal static object DefaultComparer(Type type)
        {
            return typeof(EqualityComparer<>).MakeGenericType(type).GetProperty(nameof(EqualityComparer<object>.Default)).GetValue(null);
        }

        internal static object DistinctComparer(Type tResult)
        {
            if (tResult.IsDictionary())
            {
                return Activator.CreateInstance(typeof(DictionaryComparer<,>).MakeGenericType(tResult.GetGenericArguments()));
            }

            if (tResult == typeof(object))
            {
                return new ExpandoComparer();
            }

            return DefaultComparer(tResult);
        }

        private static Expression Scoped(SelectNode select, ParameterExpression parameter, Func<Expression> body)
        {
            var scopes = Scopes(select);
            var materialized = new List<(ParameterExpression Value, Expression Body)>();
            scopes.Add((parameter, materialized));

            Expression result;
            try
            {
                result = body();
            }
            finally
            {
                scopes.RemoveAt(scopes.Count - 1);
            }

            for (var i = materialized.Count - 1; i >= 0; i--)
            {
                var (value, computed) = materialized[i];
                var inner = result;
                result = SelectMany(Select(Unit(), typeof(int), value.Type, _ => computed), value.Type, ElementOf(inner.Type), _ => inner, value);
            }

            return result;
        }

        private static List<(ParameterExpression Parameter, List<(ParameterExpression Value, Expression Body)> Materialized)> Scopes(SelectNode select)
        {
            while (select.EnclosingSelect() != null)
            {
                select = select.EnclosingSelect();
            }

            return select.Scopes;
        }

        private static MethodCallExpression Unit()
        {
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Repeat), new[] { typeof(int) }, Expression.Constant(0), Expression.Constant(1));
        }

        private static MethodCallExpression SelectMany(Expression source, Type element, Type result, Func<ParameterExpression, Expression> selector, ParameterExpression item)
        {
            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.SelectMany),
                new[] { element, result },
                source,
                Expression.Lambda(Expression.GetFuncType(element, Sequence(result)), selector(item), item));
        }
    }
}
