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
    }
}
