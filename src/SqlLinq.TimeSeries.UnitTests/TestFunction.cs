using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SqlLinq.TimeSeries.UnitTests
{
    sealed class TestFunction : ISqlFunction
    {
        private readonly Func<IReadOnlyList<SqlArgument>, Expression> _compile;

        public TestFunction(string name, Func<IReadOnlyList<SqlArgument>, Expression> compile)
        {
            Name = name;
            _compile = compile;
        }

        public string Name { get; }

        public Expression Compile(IReadOnlyList<SqlArgument> arguments)
        {
            return _compile(arguments);
        }
    }
}
