using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace SqlLinq.UnitTests
{
    static class Orville
    {
        public static List<CrewMember> Crew()
        {
            return
            [
                new CrewMember { Name = "Ed Mercer", Species = "Human", Rank = 5, Age = 41, Pay = 5.5m, ShipId = 1 },
                new CrewMember { Name = "Kelly Grayson", Species = "Human", Rank = 4, Age = 40, Pay = 4.5m, ShipId = 1 },
                new CrewMember { Name = "Bortus", Species = "Moclan", Rank = 3, Age = 45, Pay = 3m, ShipId = 1 },
                new CrewMember { Name = "Gordon Malloy", Species = "Human", Rank = 2, Age = 34, Pay = 2.5m, ShipId = 1 },
                new CrewMember { Name = "Talla Keyali", Species = "Xelayan", Rank = 3, Age = 30, Pay = 3.5m, ShipId = 2 },
                new CrewMember { Name = "Isaac", Species = "Kaylon", Rank = 2, Age = 200, Pay = 2m, ShipId = null },
            ];
        }

        public static List<IDictionary<string, object>> CrewDictionaries()
        {
            return Crew()
                .Select(c => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    ["Name"] = c.Name,
                    ["Species"] = c.Species,
                    ["Rank"] = c.Rank,
                    ["Age"] = c.Age,
                    ["Pay"] = c.Pay,
                    ["ShipId"] = c.ShipId,
                })
                .ToList();
        }

        public static List<Ship> Ships()
        {
            return
            [
                new Ship { Id = 1, Name = "Orville", Class = "Exploratory" },
                new Ship { Id = 2, Name = "Roosevelt", Class = "Exploratory" },
                new Ship { Id = 3, Name = "Pembroke", Class = "Science" },
            ];
        }

        public static List<IDictionary<string, object>> ShipDictionaries()
        {
            return Ships()
                .Select(s => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    ["Id"] = s.Id,
                    ["Name"] = s.Name,
                    ["Class"] = s.Class,
                })
                .ToList();
        }

        public static List<IDictionary<string, object>> Classes()
        {
            return
            [
                new Dictionary<string, object> { ["Class"] = "Exploratory", ["Role"] = "Exploration" },
                new Dictionary<string, object> { ["Class"] = "Science", ["Role"] = "Research" },
            ];
        }

        public static List<Planet> Planets()
        {
            return
            [
                new Planet { Name = "Earth", Species = "Human", Gravity = 1.0, Moons = 1 },
                new Planet { Name = "Moclus", Species = "Moclan", Gravity = 0.75, Moons = 0 },
                new Planet { Name = "Xelaya", Species = "Xelayan", Gravity = 3.5, Moons = 2 },
                new Planet { Name = "Kaylon 1", Species = "Kaylon", Gravity = 1.25, Moons = 3 },
            ];
        }

        public static List<IDictionary<string, object>> PlanetDictionaries()
        {
            return Planets()
                .Select(p => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    ["Name"] = p.Name,
                    ["Species"] = p.Species,
                    ["Gravity"] = p.Gravity,
                    ["Moons"] = p.Moons,
                })
                .ToList();
        }

        public static List<Duty> Duties(List<CrewMember> crew)
        {
            return
            [
                new Duty { Post = "Captain", Officer = crew[0] },
                new Duty { Post = "First Officer", Officer = crew[1] },
                new Duty { Post = "Second Officer", Officer = crew[2] },
                new Duty { Post = "Weapons", Officer = crew[2] },
                new Duty { Post = "Helm", Officer = crew[3] },
                new Duty { Post = "Security", Officer = crew[4] },
                new Duty { Post = "Navigation", Officer = null },
            ];
        }

        public static List<ExpandoObject> CrewExpandos()
        {
            return CrewDictionaries().Select(Expando).ToList();
        }

        public static Dictionary<string, IEnumerable> Tables()
        {
            var crew = Crew();

            return new Dictionary<string, IEnumerable>
            {
                ["crew"] = crew,
                ["ships"] = Ships(),
                ["classes"] = Classes(),
                ["planets"] = Planets(),
                ["duties"] = Duties(crew),
            };
        }

        public static Dictionary<string, IEnumerable> DictionaryTables()
        {
            return new Dictionary<string, IEnumerable>
            {
                ["crew"] = CrewDictionaries(),
                ["ships"] = ShipDictionaries(),
                ["classes"] = Classes(),
                ["planets"] = PlanetDictionaries(),
            };
        }

        public static string Rows<T1, T2>(IEnumerable<System.Tuple<T1, T2>> rows)
        {
            return string.Join("; ", rows.Select(r => r.Item1 + "/" + r.Item2));
        }

        public static string Rows<T1, T2, T3>(IEnumerable<System.Tuple<T1, T2, T3>> rows)
        {
            return string.Join("; ", rows.Select(r => r.Item1 + "/" + r.Item2 + "/" + r.Item3));
        }

        private static ExpandoObject Expando(IDictionary<string, object> values)
        {
            var expando = new ExpandoObject();
            var fields = (IDictionary<string, object>)expando;
            foreach (var value in values)
            {
                fields.Add(value);
            }

            return expando;
        }
    }
}
