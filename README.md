[![CI](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml/badge.svg)](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqlLinq)](https://www.nuget.org/packages/SqlLinq)

# SqlLinq

Dynamic .NET SQL queries

This project started as an experiment that allowed me to write dynamic SQL-like inline queries so I could explore the nature of my collection of MP3 files. This original project is on CodeProject: http://www.codeproject.com/Articles/28163/SqlLinq-Taking-LINQ-to-SQL-in-the-Other-Direction.

That original code was refined to being a basic implementation of SQL select statements. That code was also published and described on CodeProject: http://www.codeproject.com/Articles/43678/Dynamically-evaluated-SQL-LINQ-queries

It isn't a replacement for Linq but it can come in handy when the data model being queried or the exact structure of one's query results aren't known at compile time.

Version 2.0 updates SqlLinq for .NET Standard, and adds extensibility points to add custom SQL functions and syntax.

Thanks to [dkackman](https://github.com/dkackman) for creating SqlLinq and for letting it carry on here.

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
// "that" is the joined collection.
var members = people.Query<Person, Family, IDictionary<string, object>>(
    "SELECT Name, that.Name AS Family FROM this INNER JOIN that ON this.Address = that.Address",
    families);
```
