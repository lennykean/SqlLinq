using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class SqlExpressionTests
    {
        private static Dictionary<string, object> Row(object malloy, object lamarr)
        {
            return new Dictionary<string, object>() { { "malloy", malloy }, { "lamarr", lamarr } };
        }

        [TestMethod]
        public void TypedArithmetic()
        {
            // Arrange
            var values = new Dictionary<string, double>() { { "agg0", 5.5 }, { "agg1", 2 } };
            var difference = SqlExpression.Compile<IDictionary<string, double>>("agg0 - agg1");

            // Act
            var result = difference(values);

            // Assert
            Assert.AreEqual(3.5, result);
        }

        [TestMethod]
        public void BoxedArithmetic()
        {
            // Arrange
            var scaled = SqlExpression.Compile<IDictionary<string, object>>("(malloy - lamarr) * 1.5");

            // Act
            var doublesResult = scaled(Row(4.5, 2.5));
            var decimalAndIntResult = scaled(Row(4.5m, 2));
            var nullMalloyResult = scaled(Row(null, 2.5));
            var nullLamarrResult = scaled(Row(4.5, null));

            // Assert
            Assert.AreEqual(3.0, doublesResult);
            Assert.AreEqual(3.75, decimalAndIntResult);
            Assert.IsNull(nullMalloyResult);
            Assert.IsNull(nullLamarrResult);
        }

        [TestMethod]
        public void BoxedArithmeticOnNonNumberThrowsSqlException()
        {
            // Arrange
            var sum = SqlExpression.Compile<IDictionary<string, object>>("malloy + lamarr");

            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => sum(Row("1", 2)));
            Assert.AreEqual("Arithmetic can't use String values because they aren't numbers.", e.Message);
            Assert.ThrowsExactly<SqlException>(() => sum(Row(true, 2)));
            Assert.ThrowsExactly<SqlException>(() => sum(Row('1', 2)));
            Assert.ThrowsExactly<SqlException>(() => sum(Row(DayOfWeek.Monday, 2)));
        }

        [TestMethod]
        public void NegateBoxedValue()
        {
            // Arrange
            var negate = SqlExpression.Compile<IDictionary<string, object>>("-malloy");

            // Act
            var doubleResult = negate(Row(2.5, null));
            var intResult = negate(Row(2, null));
            var nullResult = negate(Row(null, null));

            // Assert
            Assert.AreEqual(-2.5, doubleResult);
            Assert.AreEqual(-2, intResult);
            Assert.IsNull(nullResult);
        }

        [TestMethod]
        public void NegateTypedTimeSpan()
        {
            // Arrange
            var negate = SqlExpression.Compile<DateTimeOffset>("-Offset");

            // Act
            var result = negate(new DateTimeOffset(2379, 9, 10, 0, 0, 0, TimeSpan.FromHours(3)));

            // Assert
            Assert.AreEqual(TimeSpan.FromHours(-3), result);
        }

        [TestMethod]
        public void ParseErrorEndsWithExpression()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<IDictionary<string, object>>("malloy +"));
            Assert.IsTrue(e.Message.StartsWith("SQL parse error:", StringComparison.Ordinal));
            Assert.IsTrue(e.Message.EndsWith("\nin expression\n\tmalloy +", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CompileErrorEndsWithExpression()
        {
            // Act & Assert
            var twice = new TestFunction("twice", arguments => System.Linq.Expressions.Expression.Convert(arguments[0].Expression, typeof(int)));
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<Person>("twice(Name) * 2", [twice]));
            Assert.IsTrue(e.Message.StartsWith("SQL compile error:", StringComparison.Ordinal));
            Assert.IsTrue(e.Message.EndsWith("\nin expression\n\ttwice(Name) * 2", StringComparison.Ordinal));
            Assert.IsNotNull(e.InnerException);
        }

        [TestMethod]
        [DataRow("abs(malloy > 1", "The query can't be read because a function call has no closing parenthesis.")]
        [DataRow("abs('malloy)", "The query can't be read because a string has no closing quote.")]
        public void UnclosedTextSaysWhatIsUnclosed(string expression, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<IDictionary<string, object>>(expression));
            Assert.AreEqual(message, e.Message);
        }

        [TestMethod]
        public void UnknownFieldsAreErrorsAtTheirName()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => SqlExpression.Compile<Person>("Weight + Height * 2"));
            Assert.AreEqual("Height can't be read because it isn't a field of Person.", e.Message);
            Assert.AreEqual(SqlErrorKind.UnknownName, e.Kind);
            Assert.AreEqual("Weight + ".Length, e.Start);
            Assert.AreEqual("Height".Length, e.Length);
        }
    }
}
