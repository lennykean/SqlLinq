using System.Collections.Generic;
using System.Linq.Expressions;

namespace SqlLinq
{
    public interface ISqlFunction
    {
        string Name { get; }

        Expression Compile(IReadOnlyList<SqlArgument> arguments);
    }
}
