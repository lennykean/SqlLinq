[![CI](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml/badge.svg)](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqlLinq)](https://www.nuget.org/packages/SqlLinq)

# SqlLinq

SqlLinq runs SQL `SELECT` statements over in-memory .NET collections. Queries are plain text, so they can come from a user, a config file or a saved report, and are compiled into LINQ expression trees. Queries can work with typed objects, dictionaries and dynamic objects.

The [original SqlLinq](https://github.com/dkackman/SqlLinq) was created by [dkackman](https://github.com/dkackman), and he kindly let it carry on here. Since then it has been:

- modernized and built on .NET Standard
- given support for more SQL syntax, clauses and functions
- made extensible, so custom functions can be added
- security hardened, with timeouts and cancellation, and stricter control over what can be accessed within a query
- extended with new packages to support a time series SQL dialect ([SqlLinq.TimeSeries](https://github.com/lennykean/SqlLinq/blob/master/src/SqlLinq.TimeSeries/README.md)), and unit conversions with [UnitsNet](https://github.com/angularsen/UnitsNet) ([SqlLinq.Units](https://github.com/lennykean/SqlLinq/blob/master/src/SqlLinq.Units/README.md))

## Quick start

```sh
dotnet add package SqlLinq
```

```csharp
using SqlLinq;

// Query() can select over typed objects, dictionaries, or dynamic objects
// queries support WHERE, GROUP BY, HAVING, ORDER BY, aggregates, and joins.
// "this" is the source collection.
var adults = people.Query("SELECT * FROM this WHERE Age >= 18 ORDER BY Name");
```

### Joins

```csharp
// Each entry is a table, and its key is the table's name in the query
var tables = new Dictionary<string, IEnumerable>
{
    ["people"] = people,
    ["families"] = families,
    ["cities"] = cities,
};

var members = tables.Query(
    """
    SELECT p.Name, f.Name AS Family, c.Name AS City
    FROM people p
    JOIN families f ON p.FamilyId = f.Id
    LEFT JOIN cities c ON f.CityId = c.Id
    WHERE p.Age >= 18
    """);
```

```csharp
// Two collections can also be joined directly, as "this" and "that"
var members = people.Query<Person, Family, IDictionary<string, object>>(
    "SELECT this.Name, that.Name AS Family FROM this INNER JOIN that ON this.Address = that.Address",
    families);
```

### Math functions

```csharp
// Standard SQL math functions such as abs, round, power, sqrt, log and the trigonometric functions are built in
var near = people.Query("SELECT * FROM this WHERE abs(Weight - 150) <= 20");
```
