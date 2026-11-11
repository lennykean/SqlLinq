using System.Linq.Expressions;

namespace SqlLinq.TimeSeries
{
    delegate Expression Aggregate(Expression rows, Expression window);
}
