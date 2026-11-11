using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class FunctionTests
    {
        private static readonly ISqlFunction[] Twice =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
        ];

        [TestMethod]
        public void FunctionCompilesIntoTheQuery()
        {
            // Arrange
            var source = TestData.GetPeople();

            // Act
            var result = source.Query("SELECT * FROM this WHERE twice(Weight) > 300", Twice);

            // Assert
            CollectionAssert.AreEqual(source.Where(p => p.Weight * 2 > 300).ToList(), result.ToList());
        }

        [TestMethod]
        public void NestedCallsCompile()
        {
            // Arrange
            var source = TestData.GetPeople();

            // Act
            var result = source.Query("SELECT * FROM this WHERE twice(twice(Weight)) > 600", Twice);

            // Assert
            CollectionAssert.AreEqual(source.Where(p => p.Weight * 4 > 600).ToList(), result.ToList());
        }

        [TestMethod]
        public void FunctionNamesAreCaseInsensitive()
        {
            // Arrange
            var source = TestData.GetPeople();

            // Act
            var result = source.Query("SELECT * FROM this WHERE TWICE(Weight) > 300", Twice);

            // Assert
            CollectionAssert.AreEqual(source.Where(p => p.Weight * 2 > 300).ToList(), result.ToList());
        }

        [TestMethod]
        public void CallsInsideStringsAreLeftAlone()
        {
            // Arrange
            var source = TestData.GetPeople();

            // Act
            var result = source.Query("SELECT * FROM this WHERE Name = 'twice(Weight)'", Twice);

            // Assert
            Assert.IsEmpty(result.ToList());
        }

        [TestMethod]
        public void ArgumentsCarryTheirText()
        {
            // Arrange
            var seen = new List<string>();
            var functions = new ISqlFunction[]
            {
                new TestFunction("check", arguments =>
                {
                    seen.AddRange(arguments.Select(a => a.Text));
                    return Expression.Constant(true);
                }),
            };

            // Act
            var result = TestData.GetPeople().Query("SELECT * FROM this WHERE check(Weight, 'kg')", functions).ToList();

            // Assert
            Assert.HasCount(TestData.GetPeople().Count(), result);
            Assert.AreEqual("Weight, 'kg'", string.Join(", ", seen));
        }

        [TestMethod]
        public void SqlExpressionCompilesFunctions()
        {
            // Arrange
            var person = TestData.GetPeople().First();

            // Act
            var twice = SqlExpression.Compile<Person>("twice(Weight)", Twice);

            // Assert
            Assert.AreEqual(person.Weight * 2, twice(person));
        }

        [TestMethod]
        public void MathFunctionsWorkInQueriesWithoutRegistering()
        {
            // Arrange
            var source = TestData.GetPeople();

            // Act
            var near = source.Query("SELECT * FROM this WHERE abs(Weight - 150) <= 20").ToList();
            var rounded = source.Query<Person, IDictionary<string, object>>("SELECT Name, round(Weight, -2) AS Rounded FROM this WHERE Name = 'Bill'").Single();

            // Assert
            CollectionAssert.AreEqual(source.Where(p => Math.Abs(p.Weight - 150) <= 20).ToList(), near);
            Assert.AreEqual(200, rounded["Rounded"]);
        }

        [TestMethod]
        [DataRow("abs(-2)", 2)]
        [DataRow("sign(-3)", -1)]
        [DataRow("mod(7, 3)", 1)]
        [DataRow("mod(-7, 3)", -1)]
        [DataRow("round(7)", 7)]
        [DataRow("round(7, 1)", 7)]
        [DataRow("round(1250, -2)", 1300)]
        [DataRow("floor(7)", 7)]
        [DataRow("ceil(7)", 7)]
        [DataRow("ceiling(7)", 7)]
        public void MathFunctionsKeepWholeNumbersWhole(string expression, int expected)
        {
            // Act
            var result = SqlExpression.Compile<Person>(expression)(TestData.GetPeople().First());

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MathFunctionsKeepLongsAndTurnOtherNumbersIntoDoubles()
        {
            // Arrange
            var abs = SqlExpression.Compile<IDictionary<string, object>>("abs(Value)");

            // Act
            var longResult = abs(new Dictionary<string, object> { ["Value"] = -2L });
            var decimalResult = abs(new Dictionary<string, object> { ["Value"] = -2.5m });
            var floatResult = abs(new Dictionary<string, object> { ["Value"] = -2.5f });

            // Assert
            Assert.AreEqual(2L, longResult);
            Assert.AreEqual(2.5, decimalResult);
            Assert.AreEqual(2.5, floatResult);
        }

        [TestMethod]
        [DataRow("abs(-2.5)", 2.5)]
        [DataRow("sign(-2.5)", -1.0)]
        [DataRow("mod(7.5, 2)", 1.5)]
        [DataRow("round(2.5)", 3.0)]
        [DataRow("round(-2.5)", -3.0)]
        [DataRow("round(1.25, 1)", 1.3)]
        [DataRow("round(1250.0, -2)", 1300.0)]
        [DataRow("floor(-1.5)", -2.0)]
        [DataRow("ceil(1.2)", 2.0)]
        [DataRow("ceiling(-1.2)", -1.0)]
        [DataRow("power(2, 10)", 1024.0)]
        [DataRow("sqrt(16)", 4.0)]
        [DataRow("exp(1)", Math.E)]
        [DataRow("ln(exp(2))", 2.0)]
        [DataRow("log10(1000)", 3.0)]
        [DataRow("log(2, 8)", 3.0)]
        [DataRow("pi()", Math.PI)]
        [DataRow("degrees(pi())", 180.0)]
        [DataRow("radians(180)", Math.PI)]
        [DataRow("sin(pi() / 2)", 1.0)]
        [DataRow("cos(pi())", -1.0)]
        [DataRow("tan(pi() / 4)", 1.0)]
        [DataRow("asin(1)", Math.PI / 2)]
        [DataRow("acos(-1)", Math.PI)]
        [DataRow("atan(1)", Math.PI / 4)]
        [DataRow("atan2(1, -1)", 3 * Math.PI / 4)]
        public void MathFunctionsCompute(string expression, double expected)
        {
            // Act
            var result = SqlExpression.Compile<Person>(expression)(TestData.GetPeople().First());

            // Assert
            Assert.AreEqual(expected, (double)result, 1e-12);
        }

        [TestMethod]
        [DataRow("abs(Value)")]
        [DataRow("abs(NULL)")]
        [DataRow("mod(Value, 3)")]
        [DataRow("round(Value, 1)")]
        [DataRow("round(1.5, Value)")]
        [DataRow("sqrt(Value)")]
        [DataRow("log(Value, 8)")]
        [DataRow("atan2(1, Value)")]
        public void MathFunctionsOfNullAreNull(string expression)
        {
            // Arrange
            var row = new Dictionary<string, object> { ["Value"] = null };

            // Act
            var result = SqlExpression.Compile<IDictionary<string, object>>(expression)(row);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [DataRow("abs()", "abs takes one value, e.g. abs(RPM).")]
        [DataRow("ABS(1, 2)", "abs takes one value, e.g. abs(RPM).")]
        [DataRow("mod(7)", "mod takes two values, e.g. mod(RPM, 1000).")]
        [DataRow("round(1, 2, 3)", "round takes one or two values, e.g. round(AFR) or round(AFR, 1).")]
        [DataRow("log(8)", "log takes two values, e.g. log(2, RPM).")]
        [DataRow("pi(1)", "pi takes no values, e.g. pi().")]
        public void MathFunctionArgumentCountsAreChecked(string expression, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<Person>(expression));
            Assert.AreEqual(message, e.Message);
        }

        [TestMethod]
        public void RegisteredFunctionReplacesTheBuiltIn()
        {
            // Arrange
            var functions = new ISqlFunction[] { new TestFunction("ABS", arguments => Expression.Constant(-1)) };

            // Act
            var abs = SqlExpression.Compile<Person>("abs(Weight)", functions);

            // Assert
            Assert.AreEqual(-1, abs(TestData.GetPeople().First()));
        }
    }
}
