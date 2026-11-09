using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class SingleValueSubqueryTests
    {
        private static readonly ISqlFunction[] Functions =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
            new TestFunction("upper", arguments => Expression.Call(Expression.Convert(arguments[0].Expression, typeof(string)), typeof(string).GetMethod(nameof(string.ToUpperInvariant), Type.EmptyTypes))),
        ];

        [TestMethod]
        [DataRow("SELECT Name, Species FROM crew WHERE ShipId = (SELECT Id FROM ships WHERE Name = 'Orville') AND Rank < 3", "Gordon Malloy/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = (SELECT COUNT(*) FROM ships)", "Bortus/Moclan; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id = (SELECT ShipId FROM crew WHERE Name = 'Talla Keyali')", "Roosevelt/Exploratory")]
        [DataRow("SELECT Name, Species FROM crew WHERE Species = (SELECT Species FROM planets WHERE Moons = 0)", "Bortus/Moclan")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Class = (SELECT k.Class FROM classes k WHERE k.Role = 'Exploration') AND c.Rank > 3", "Ed Mercer/Orville; Kelly Grayson/Orville")]
        [DataRow("SELECT Name, Species FROM crew WHERE (SELECT COUNT(*) FROM ships WHERE Class = 'Warship') = 0 AND Rank = 5", "Ed Mercer/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE (SELECT Name FROM ships WHERE Class = 'Warship') IS NOT NULL", "")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = (SELECT COUNT(*) FROM ships WHERE Class = 'Exploratory') + 1", "Bortus/Moclan; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > 3 OR Species = (SELECT Species FROM planets WHERE Name = 'Xelaya')", "Ed Mercer/Human; Kelly Grayson/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Species IN ((SELECT Species FROM planets WHERE Moons = 0))", "Bortus/Moclan")]
        public void SubqueriesReadAnyTableInEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Age = (SELECT MIN(c2.Age) FROM crew c2)", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Age = (SELECT MAX(Age) FROM crew)", "Isaac/Kaylon")]
        [DataRow("SELECT Name, Species FROM crew WHERE (SELECT MAX(Rank) FROM crew) = Rank", "Ed Mercer/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = (SELECT MAX(Rank) FROM crew) - 1", "Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Age BETWEEN (SELECT MIN(Age) FROM crew) AND 35", "Gordon Malloy/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay > (SELECT AVG(Pay) FROM crew)", "Ed Mercer/Human; Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Pay = (SELECT MAX(Pay) FROM crew WHERE Species = 'Xelayan')", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Species = (SELECT p.Species FROM planets p WHERE p.Gravity = (SELECT MAX(Gravity) FROM planets))", "Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > (SELECT AVG(Rank) FROM crew) AND Age < (SELECT MAX(Age) FROM crew WHERE Species = 'Human')", "Kelly Grayson/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank > (SELECT AVG(Rank) FROM crew) OR Age = (SELECT MIN(Age) FROM crew)", "Ed Mercer/Human; Kelly Grayson/Human; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE NOT Rank < (SELECT MAX(Rank) FROM crew)", "Ed Mercer/Human")]
        [DataRow("SELECT Name, Species FROM crew WHERE Rank = (SELECT MAX(Moons) FROM planets)", "Bortus/Moclan; Talla Keyali/Xelayan")]
        [DataRow("SELECT Name, Species FROM crew WHERE Age IN ((SELECT MAX(Age) FROM crew))", "Isaac/Kaylon")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Id = (SELECT MAX(Id) FROM ships WHERE Class = 'Exploratory')", "Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c, ships s WHERE c.ShipId = s.Id AND c.Age = (SELECT MAX(Age) FROM crew WHERE ShipId IS NOT NULL)", "Bortus/Orville")]
        public void AggregateSubqueriesReadTypedTables(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT k.Role FROM classes k WHERE k.Class = s.Class) = 'Research'", "Pembroke/Science")]
        [DataRow("SELECT Name, Class FROM ships WHERE (SELECT COUNT(*) FROM crew WHERE crew.ShipId = ships.Id) = 0", "Pembroke/Science")]
        [DataRow("SELECT p.Name, p.Species FROM planets p WHERE (SELECT COUNT(*) FROM crew c WHERE c.Species = p.Species) > 1", "Earth/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT p.Name FROM planets p WHERE p.Species = c.Species) = 'Xelaya'", "Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT s.Name FROM ships s WHERE s.Id = c.ShipId) IS NULL", "Isaac/Kaylon")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE (SELECT k.Role FROM classes k WHERE k.Class = s.Class) = 'Exploration' AND c.Rank = 5", "Ed Mercer/Orville")]
        [DataRow("SELECT c.Name, c.Species FROM crew c, ships s WHERE c.ShipId = s.Id AND (SELECT COUNT(*) FROM crew c2 WHERE c2.ShipId = s.Id) = 1", "Talla Keyali/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT COUNT(*) FROM planets p WHERE p.Species = c.Species AND p.Moons > 1) = 1", "Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT k.Class, k.Role FROM classes k WHERE (SELECT COUNT(*) FROM ships s WHERE s.Class = k.Class AND (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id) > 0) = 2", "Exploratory/Exploration")]
        [DataRow("SELECT k.Class, k.Role FROM classes k WHERE (SELECT COUNT(*) FROM ships s WHERE s.Class = k.Class AND (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id AND k.Role = 'Research') = 0) = 1", "Science/Research")]
        [DataRow("SELECT p.Name, p.Species FROM planets p WHERE (SELECT COUNT(*) FROM ships s WHERE (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id AND c.Species = p.Species) > 0) = 1", "Earth/Human; Moclus/Moclan; Xelaya/Xelayan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT s.Class FROM ships s WHERE s.Id = c.ShipId) = (SELECT k.Class FROM classes k WHERE k.Role = 'Exploration') AND c.Rank < 3", "Gordon Malloy/Human")]
        public void CorrelatedSubqueriesReadEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, string>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Age > (SELECT AVG(c2.Age) FROM crew c2 WHERE c2.ShipId = c.ShipId)", "Ed Mercer/Human; Bortus/Moclan")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Age = (SELECT MIN(c2.Age) FROM crew c2 WHERE c2.Species = c.Species)", "Bortus/Moclan; Gordon Malloy/Human; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Age = (SELECT MIN(c2.Age) FROM crew c2 WHERE c2.ShipId = s.Id)", "Gordon Malloy/Orville; Talla Keyali/Roosevelt")]
        [DataRow("SELECT c.Name, p.Name AS Homeworld FROM crew c JOIN planets p ON c.Species = p.Species WHERE c.Rank = (SELECT MAX(c2.Rank) FROM crew c2 WHERE c2.Species = p.Species)", "Ed Mercer/Earth; Bortus/Moclus; Talla Keyali/Xelaya; Isaac/Kaylon 1")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT MAX(c.Rank) FROM crew c WHERE c.ShipId = s.Id) = 3", "Roosevelt/Exploratory")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE c.Rank = (SELECT MAX(c2.Rank) FROM crew c2 WHERE c2.ShipId = c.ShipId) - 1", "Kelly Grayson/Human")]
        [DataRow("SELECT c.Name, c.Species FROM crew c WHERE (SELECT COUNT(*) FROM crew c2 WHERE c2.Species = c.Species AND c2.Rank > c.Rank) = 0", "Ed Mercer/Human; Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT s.Name, s.Class FROM ships s WHERE (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id AND c.Age > (SELECT AVG(c2.Age) FROM crew c2 WHERE c2.ShipId = s.Id)) = 2", "Orville/Exploratory")]
        [DataRow("SELECT p.Name, p.Species FROM planets p WHERE p.Moons + 1 = (SELECT MAX(c.Rank) FROM crew c WHERE c.Species = p.Species)", "Xelaya/Xelayan")]
        public void CorrelatedAggregateSubqueriesReadTypedTables(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT t.Name, t.Species FROM this t WHERE (SELECT COUNT(*) FROM this c WHERE c.Species = t.Species) = 1", "Bortus/Moclan; Talla Keyali/Xelayan; Isaac/Kaylon")]
        [DataRow("SELECT t.Name, t.Species FROM this t WHERE t.Age < (SELECT AVG(c.Age) FROM this c WHERE c.Species = t.Species)", "Gordon Malloy/Human")]
        public void CorrelatedSubqueriesReadTheQuerySource(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        [DataRow("SELECT Species, COUNT(*) FROM crew GROUP BY Species HAVING COUNT(*) > (SELECT COUNT(*) FROM ships) - 2", "Human/3")]
        [DataRow("SELECT s.Class, COUNT(*) FROM ships s GROUP BY s.Class HAVING COUNT(*) = (SELECT COUNT(*) FROM classes)", "Exploratory/2")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) = (SELECT COUNT(*) FROM ships s WHERE s.Class = 'Science') ORDER BY c.Species", "Kaylon/1; Moclan/1; Xelayan/1")]
        public void SubqueriesFilterGroupsInEveryRowShape(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<Tuple<string, int>>(sql);
            var dictionaries = Orville.DictionaryTables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(typed), "typed tables");
            Assert.AreEqual(expected, Orville.Rows(dictionaries), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT s.Class, COUNT(*) FROM ships s GROUP BY s.Class HAVING (SELECT k.Role FROM classes k WHERE k.Class = s.Class) = 'Research'", "Science/1")]
        [DataRow("SELECT Class, COUNT(*) FROM ships GROUP BY Class HAVING (SELECT k.Role FROM classes k WHERE k.Class = ships.Class) = 'Exploration'", "Exploratory/2")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING (SELECT p.Moons FROM planets p WHERE p.Species = c.Species) > 1 ORDER BY c.Species", "Kaylon/1; Xelayan/1")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING (SELECT COUNT(*) FROM planets p WHERE p.Species = c.Species AND p.Moons = 1) = 1", "Human/3")]
        [DataRow("SELECT c.Species, COUNT(*) FROM crew c GROUP BY c.Species HAVING COUNT(*) = 1 AND (SELECT p.Name FROM planets p WHERE p.Species = c.Species) LIKE 'X%'", "Xelayan/1")]
        public void CorrelatedSubqueriesFilterGroupsIntoDictionaries(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query(sql);
            var dictionaries = Orville.DictionaryTables().Query(sql);

            // Assert
            Assert.AreEqual(expected, string.Join("; ", typed.Select(r => string.Join("/", r.Values))), "typed tables");
            Assert.AreEqual(expected, string.Join("; ", dictionaries.Select(r => string.Join("/", r.Values))), "dictionary tables");
        }

        [TestMethod]
        [DataRow("SELECT Species, MAX(Age) FROM crew GROUP BY Species HAVING MAX(Age) > (SELECT MAX(Moons) FROM planets) * 15", "Kaylon/200")]
        [DataRow("SELECT Species, SUM(Rank) FROM crew GROUP BY Species HAVING SUM(Rank) > (SELECT MAX(Id) FROM ships) ORDER BY Species", "Human/11")]
        [DataRow("SELECT c.Species, MIN(c.Age) FROM crew c GROUP BY c.Species HAVING MIN(c.Age) < (SELECT MAX(s.Id) FROM ships s) * 10 + 5 ORDER BY c.Species", "Human/34; Xelayan/30")]
        public void SubqueriesFilterAggregateGroups(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, int>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void SubqueriesFilterAnUngroupedAggregate()
        {
            // Act
            var result = Orville.Tables().Query<int>("SELECT COUNT(*) FROM crew HAVING COUNT(*) = (SELECT COUNT(*) FROM ships) * 2");

            // Assert
            Assert.AreEqual("6", string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT Name FROM this WHERE twice(Rank) > (SELECT MAX(Rank) FROM this)", "Ed Mercer, Kelly Grayson, Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE twice(Rank) = (SELECT MAX(Rank) FROM this) + 1", "Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE Rank > (SELECT AVG(Rank) FROM this WHERE twice(Age) > 100)", "Ed Mercer, Kelly Grayson, Bortus, Talla Keyali")]
        [DataRow("SELECT Name FROM this WHERE upper(Species) = 'MOCLAN' OR Rank = (SELECT MAX(Rank) FROM this WHERE upper(Name) LIKE 'K%')", "Kelly Grayson, Bortus")]
        public void SubqueriesCombineWithFunctions(string sql, string expected)
        {
            // Act
            var result = Orville.Crew().Query<CrewMember, string>(sql, Functions);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", result));
        }

        [TestMethod]
        [DataRow("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE twice(c.Rank) > (SELECT COUNT(*) FROM ships) * 2", "Ed Mercer, Kelly Grayson")]
        [DataRow("SELECT s.Name FROM ships s WHERE (SELECT COUNT(*) FROM crew c WHERE c.ShipId = s.Id AND twice(c.Rank) >= 8) = 2", "Orville")]
        [DataRow("SELECT Name FROM crew WHERE twice(Rank) = (SELECT COUNT(*) FROM ships) * 2", "Bortus, Talla Keyali")]
        [DataRow("SELECT c.Name FROM crew c, planets p WHERE c.Species = p.Species AND upper(p.Name) = 'XELAYA' AND c.Rank = (SELECT COUNT(*) FROM ships)", "Talla Keyali")]
        public void TableSubqueriesCombineWithFunctions(string sql, string expected)
        {
            // Act
            var typed = Orville.Tables().Query<string>(sql, Functions);
            var dictionaries = Orville.DictionaryTables().Query<string>(sql, Functions);

            // Assert
            Assert.AreEqual(expected, string.Join(", ", typed), "typed tables");
            Assert.AreEqual(expected, string.Join(", ", dictionaries), "dictionary tables");
        }

        [TestMethod]
        public void FunctionArgumentsKeepTheirQualifiedText()
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
            var result = Orville.Tables().Query<string>("SELECT c.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE check(c.Rank, s.Name) AND c.Rank = 5", functions).ToList();

            // Assert
            Assert.AreEqual("Ed Mercer", string.Join(", ", result));
            Assert.AreEqual("c.Rank, s.Name", string.Join(", ", seen.Distinct()));
        }
    }
}
