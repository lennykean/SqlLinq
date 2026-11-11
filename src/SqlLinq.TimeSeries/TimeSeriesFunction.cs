using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SqlLinq.SyntaxTree;
using SqlLinq.SyntaxTree.Expressions;

namespace SqlLinq.TimeSeries
{
    sealed class TimeSeriesFunction : ISqlFunction
    {
        private TimeSeriesFunction(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Expression Compile(IReadOnlyList<SqlArgument> arguments)
        {
            throw new SqlException(Name + " can't be used here.");
        }

        public static IEnumerable<ISqlFunction> With(IEnumerable<string> names, IEnumerable<ISqlFunction> functions)
        {
            var own = names.Select(n => (ISqlFunction)new TimeSeriesFunction(n)).ToList();

            return own.Concat((functions ?? Enumerable.Empty<ISqlFunction>()).Where(f => !own.Any(o => string.Equals(o.Name, f.Name, System.StringComparison.OrdinalIgnoreCase))));
        }

        public static List<NodeWithId> Calls(ExpressionNode expression, ICollection<string> names)
        {
            var calls = new List<NodeWithId>();
            Find(expression, names, calls);

            return calls;
        }

        public static bool CallsAny(string text, ICollection<string> names)
        {
            var tokens = SqlSyntax.Tokenize(text);

            return tokens.Where((t, i) => t.Kind == SqlTokenKind.FunctionName && i + 1 < tokens.Count && tokens[i + 1].Text == "(").Any(t => names.Contains(t.Text));
        }

        public static bool Within(ExpressionNode expression)
        {
            return Calls(expression, null).Count > 0;
        }

        private static void Find(NonTerminalNode node, ICollection<string> names, List<NodeWithId> calls)
        {
            if (node is NodeWithId call && call.Function is TimeSeriesFunction && (names == null || names.Contains(call.FunctionName)))
            {
                calls.Add(call);
                return;
            }

            var arguments = (node as NodeWithId)?.Arguments ?? Enumerable.Empty<ArgumentNode>();
            foreach (var child in node.Children.OfType<NonTerminalNode>().Concat(arguments.Where(a => a.Expression != null).Select(a => a.Expression)))
            {
                Find(child, names, calls);
            }
        }
    }
}
