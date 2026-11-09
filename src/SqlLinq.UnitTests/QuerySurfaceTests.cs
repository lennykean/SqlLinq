using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlLinq.UnitTests
{
    [TestClass]
    public class QuerySurfaceTests
    {
        private const string Talla = "SELECT c.Name, s.Name AS Ship, c.Rank FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Species = 'Xelayan'";

        private static readonly ISqlFunction[] Twice =
        [
            new TestFunction("twice", arguments => Expression.Multiply(Expression.Convert(arguments[0].Expression, typeof(int)), Expression.Constant(2))),
        ];

        [TestMethod]
        public void JoinsSelectIntoEveryResultShape()
        {
            // Arrange
            var tables = Orville.Tables();

            // Act
            var dictionary = tables.Query(Talla).Single();
            var expando = tables.Query<dynamic>(Talla).Single();
            var tuple = tables.Query<Tuple<string, string, int>>(Talla).Single();
            var posting = tables.Query<Posting>(Talla).Single();
            var single = tables.Query<string>("SELECT s.Name FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 5").Single();

            // Assert
            Assert.HasCount(3, dictionary);
            Assert.AreEqual("Talla Keyali", dictionary["Name"]);
            Assert.AreEqual("Roosevelt", dictionary["Ship"]);
            Assert.AreEqual(3, dictionary["Rank"]);
            Assert.AreEqual("Talla Keyali", expando.Name);
            Assert.AreEqual("Roosevelt", expando.Ship);
            Assert.AreEqual(3, expando.Rank);
            Assert.AreEqual(Tuple.Create("Talla Keyali", "Roosevelt", 3), tuple);
            Assert.AreEqual("Talla Keyali", posting.Name);
            Assert.AreEqual("Roosevelt", posting.Ship);
            Assert.AreEqual(3, posting.Rank);
            Assert.AreEqual("Orville", single);
        }

        [TestMethod]
        public void ThisAndThatSelectIntoEveryResultShape()
        {
            // Arrange
            const string Sql = "SELECT this.Name, that.Name AS Ship, Rank FROM this JOIN that ON this.ShipId = that.Id WHERE Species = 'Xelayan'";
            var crew = Orville.Crew();
            var ships = Orville.Ships();

            // Act
            var dictionary = crew.Query<CrewMember, Ship, IDictionary<string, object>>(Sql, ships).Single();
            var expando = crew.Query<CrewMember, Ship, dynamic>(Sql, ships).Single();
            var posting = crew.Query<CrewMember, Ship, Posting>(Sql, ships).Single();
            var single = crew.Query<CrewMember, Ship, string>("SELECT that.Class FROM this JOIN that ON this.ShipId = that.Id WHERE this.Rank = 5", ships).Single();

            // Assert
            Assert.AreEqual("Roosevelt", dictionary["Ship"]);
            Assert.AreEqual(3, dictionary["Rank"]);
            Assert.AreEqual("Talla Keyali", expando.Name);
            Assert.AreEqual("Roosevelt", posting.Ship);
            Assert.AreEqual("Exploratory", single);
        }

        [TestMethod]
        public void SingleTablesSelectIntoEveryResultShape()
        {
            // Arrange
            const string Sql = "SELECT p.Species, p.Moons AS Total FROM planets p WHERE p.Gravity > 3";
            var tables = Orville.Tables();

            // Act
            var dictionary = tables.Query(Sql).Single();
            var expando = tables.Query<dynamic>(Sql).Single();
            var tuple = tables.Query<Tuple<string, int>>(Sql).Single();
            var tally = tables.Query<SpeciesTally>(Sql).Single();
            var moons = tables.Query<int>("SELECT Moons FROM planets ORDER BY Moons DESC").ToList();

            // Assert
            Assert.AreEqual("Xelayan", dictionary["Species"]);
            Assert.AreEqual(2, dictionary["Total"]);
            Assert.AreEqual(2, expando.Total);
            Assert.AreEqual(Tuple.Create("Xelayan", 2), tuple);
            Assert.AreEqual("Xelayan", tally.Species);
            Assert.AreEqual(2, tally.Total);
            Assert.AreEqual("3, 2, 1, 0", string.Join(", ", moons));
        }

        [TestMethod]
        public void SelectStarReturnsTheOriginalObjects()
        {
            // Arrange
            var tables = Orville.Tables();
            var ships = (List<Ship>)tables["ships"];
            var crew = (List<CrewMember>)tables["crew"];

            // Act
            var pembroke = tables.Query<Ship>("SELECT * FROM ships s WHERE s.Class = 'Science'").Single();
            var aliased = crew.Query("SELECT * FROM this c WHERE c.Pay > 4 ORDER BY c.Name DESC").ToList();

            // Assert
            Assert.AreSame(ships[2], pembroke);
            Assert.HasCount(2, aliased);
            Assert.AreSame(crew[1], aliased[0]);
            Assert.AreSame(crew[0], aliased[1]);
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) FROM this WHERE Species = 'Human'", 3)]
        [DataRow("SELECT MAX(Age) FROM this", 200)]
        [DataRow("SELECT MIN(Rank) FROM this WHERE Pay > 3", 3)]
        [DataRow("SELECT SUM(Age) FROM this c WHERE c.ShipId = 1", 160)]
        [DataRow("SELECT COUNT(ALL ShipId) FROM this", 5)]
        [DataRow("SELECT COUNT(DISTINCT Species) FROM this", 4)]
        [DataRow("SELECT COUNT(*) FROM this WHERE Rank > (SELECT AVG(Rank) FROM this)", 2)]
        [DataRow("SELECT COUNT(*) FROM this t WHERE t.Rank = (SELECT MAX(c.Rank) FROM this c WHERE c.Species = t.Species)", 4)]
        [DataRow("SELECT COUNT(*) FROM this WHERE Species = 'Vulcan'", 0)]
        public void ScalarQueriesReturnOneValue(string sql, int expected)
        {
            // Act
            var result = Orville.Crew().QueryScalar<CrewMember, int>(sql);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ScalarQueriesReturnTheAggregateType()
        {
            // Arrange
            var crew = Orville.Crew();

            // Act
            var pay = crew.QueryScalar<CrewMember, decimal>("SELECT SUM(Pay) FROM this");
            var topPay = crew.QueryScalar<CrewMember, decimal>("SELECT MAX(Pay) FROM this WHERE Species = 'Human'");
            var rank = crew.QueryScalar<CrewMember, double>("SELECT AVG(Rank) FROM this");
            var gravity = Orville.Planets().QueryScalar<Planet, double>("SELECT SUM(Gravity) FROM this WHERE Moons > 0");

            // Assert
            Assert.AreEqual(21m, pay);
            Assert.AreEqual(5.5m, topPay);
            Assert.AreEqual(19.0 / 6, rank, 1e-9);
            Assert.AreEqual(5.75, gravity, 1e-9);
        }

        [TestMethod]
        [DataRow("SELECT COUNT(*) FROM crew c, ships s, classes k", 36)]
        [DataRow("SELECT COUNT(*) FROM crew c JOIN planets p ON c.Species = p.Species WHERE p.Moons > 0", 5)]
        [DataRow("SELECT MAX(Moons) FROM planets WHERE Gravity < 2", 3)]
        [DataRow("SELECT COUNT(*) FROM duties d JOIN crew c ON d.Officer = c WHERE c.ShipId = 1", 5)]
        public void TablesReturnOneAggregateRow(string sql, int expected)
        {
            // Act
            var result = Orville.Tables().Query<int>(sql).ToList();

            // Assert
            Assert.HasCount(1, result);
            Assert.AreEqual(expected, result[0]);
        }

        [TestMethod]
        public void CompiledGroupQueryEvaluatesEachSourceIndependently()
        {
            // Arrange
            var query = new Query<CrewMember, Tuple<string, int>>("SELECT Species, COUNT(*) FROM this GROUP BY Species HAVING COUNT(*) > 1 ORDER BY Species");
            query.Compile();
            var crew = Orville.Crew();
            var withKlyden = Orville.Crew();
            withKlyden.Add(new CrewMember { Name = "Klyden", Species = "Moclan", Rank = 1, Age = 40, Pay = 1m });

            // Act
            var all = Orville.Rows(query.Evaluate(crew));
            var more = Orville.Rows(query.Evaluate(withKlyden));
            var none = Orville.Rows(query.Evaluate(crew.Where(c => c.Species != "Human")));
            var again = Orville.Rows(crew.Query(query));

            // Assert
            Assert.AreEqual("Human/3", all);
            Assert.AreEqual("Human/3; Moclan/2", more);
            Assert.AreEqual(string.Empty, none);
            Assert.AreEqual("Human/3", again);
        }

        [TestMethod]
        public void CompiledCorrelatedQueryReadsEachEvaluatedSource()
        {
            // Arrange
            var query = new Query<CrewMember, string>("SELECT c.Name FROM this c WHERE c.Rank = (SELECT MAX(c2.Rank) FROM this c2 WHERE c2.Species = c.Species)");
            query.Compile();

            // Act
            var humans = query.Evaluate(Orville.Crew().Where(c => c.Species == "Human"));
            var withoutEd = query.Evaluate(Orville.Crew().Skip(1));

            // Assert
            Assert.AreEqual("Ed Mercer", string.Join(", ", humans));
            Assert.AreEqual("Kelly Grayson, Bortus, Talla Keyali, Isaac", string.Join(", ", withoutEd));
        }

        [TestMethod]
        public void CompiledQueryKeepsItsFunctions()
        {
            // Arrange
            var query = new Query<CrewMember, string>("SELECT Name FROM this WHERE twice(Rank) > 7 ORDER BY Name", Twice);
            query.Compile();

            // Act
            var all = query.Evaluate(Orville.Crew());
            var withoutEd = query.Evaluate(Orville.Crew().Skip(1));

            // Assert
            Assert.AreEqual("Ed Mercer, Kelly Grayson", string.Join(", ", all));
            Assert.AreEqual("Kelly Grayson", string.Join(", ", withoutEd));
        }

        [TestMethod]
        public void CompiledDictionaryQueryEvaluatesEachSource()
        {
            // Arrange
            var query = new Query<IDictionary<string, object>, string>("SELECT Name FROM this WHERE Pay > 4 ORDER BY Name DESC");
            query.Compile();
            var crew = Orville.CrewDictionaries();

            // Act
            var all = query.Evaluate(crew);
            var first = crew.Take(1).Query(query);

            // Assert
            Assert.AreEqual("Kelly Grayson, Ed Mercer", string.Join(", ", all));
            Assert.AreEqual("Ed Mercer", string.Join(", ", first));
        }

        [TestMethod]
        public void CompiledScalarQueryEvaluatesEachSource()
        {
            // Arrange
            var query = new ScalarQuery<CrewMember, int>("SELECT MAX(Age) FROM this WHERE ShipId IS NOT NULL");
            query.Compile();

            // Act
            var all = query.Evaluate(Orville.Crew());
            var humans = Orville.Crew().Where(c => c.Species == "Human").QueryScalar(query);

            // Assert
            Assert.AreEqual(45, all);
            Assert.AreEqual(41, humans);
        }

        [TestMethod]
        public void TableKeysMatchAnAliasOrAName()
        {
            // Arrange
            var byAlias = new Dictionary<string, IEnumerable> { ["s"] = Orville.Ships(), ["c"] = Orville.Crew() };
            var byName = new Dictionary<string, IEnumerable> { ["SHIPS"] = Orville.Ships(), ["Crew"] = Orville.Crew() };
            const string Sql = "SELECT c.Name, s.Name AS Ship FROM crew c JOIN ships s ON c.ShipId = s.Id WHERE s.Id = 2";

            // Act
            var viaAlias = byAlias.Query<Tuple<string, string>>(Sql);
            var viaName = byName.Query<Tuple<string, string>>(Sql);

            // Assert
            Assert.AreEqual("Talla Keyali/Roosevelt", Orville.Rows(viaAlias));
            Assert.AreEqual("Talla Keyali/Roosevelt", Orville.Rows(viaName));
        }

        [TestMethod]
        [DataRow("SELECT Name, Species FROM planets WHERE Moons > 1", "Xelaya/Xelayan; Kaylon 1/Kaylon")]
        [DataRow("SELECT p.Name, p.Species FROM planets p WHERE p.Gravity < 1", "Moclus/Moclan")]
        [DataRow("SELECT Class, Role FROM classes WHERE Role <> 'Exploration'", "Science/Research")]
        [DataRow("SELECT Name, Class FROM ships WHERE Id = (SELECT MAX(ShipId) FROM crew)", "Roosevelt/Exploratory")]
        public void TablesPickOneTableFromMany(string sql, string expected)
        {
            // Act
            var result = Orville.Tables().Query<Tuple<string, string>>(sql);

            // Assert
            Assert.AreEqual(expected, Orville.Rows(result));
        }

        [TestMethod]
        public void TablesAcceptArraysAndDeferredSequences()
        {
            // Arrange
            var tables = new Dictionary<string, IEnumerable>
            {
                ["crew"] = Orville.Crew().ToArray(),
                ["ships"] = Orville.Ships().Where(s => s.Id < 3),
            };

            // Act
            var single = tables.Query<string>("SELECT Name FROM crew WHERE Rank > 3");
            var joined = tables.Query<Tuple<string, string>>("SELECT c.Name, s.Name AS Ship FROM crew c RIGHT JOIN ships s ON c.ShipId = s.Id WHERE c.Rank = 3");

            // Assert
            Assert.AreEqual("Ed Mercer, Kelly Grayson", string.Join(", ", single));
            Assert.AreEqual("Bortus/Orville; Talla Keyali/Roosevelt", Orville.Rows(joined));
        }

        [TestMethod]
        public void ExpandoRowsSelectIntoEveryResultShape()
        {
            // Arrange
            const string Sql = "SELECT Species, Rank AS Total FROM this WHERE Name = 'Bortus'";
            IEnumerable<dynamic> crew = Orville.CrewExpandos();

            // Act
            var dictionary = crew.Query<dynamic, IDictionary<string, object>>(Sql).Single();
            var expando = crew.Query<dynamic, dynamic>(Sql).Single();
            var tuple = crew.Query<dynamic, Tuple<string, int>>(Sql).Single();
            var tally = crew.Query<dynamic, SpeciesTally>(Sql).Single();
            var names = crew.Query<dynamic, string>("SELECT Name FROM this WHERE Rank = 2").ToList();

            // Assert
            Assert.AreEqual(3, dictionary["Total"]);
            Assert.AreEqual("Moclan", expando.Species);
            Assert.AreEqual(Tuple.Create("Moclan", 3), tuple);
            Assert.AreEqual("Moclan", tally.Species);
            Assert.AreEqual(3, tally.Total);
            Assert.AreEqual("Gordon Malloy, Isaac", string.Join(", ", names));
        }
    }
}
