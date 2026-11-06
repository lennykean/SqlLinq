[![CI](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml/badge.svg)](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqlLinq.TimeSeries)](https://www.nuget.org/packages/SqlLinq.TimeSeries)

# Time series extension for SqlLinq

[SqlLinq](https://github.com/lennykean/SqlLinq) allows using ANSI style SQL `SELECT` statements over collections.

`SqlLinq.TimeSeries` extends basic SQL queries with time series clauses and aggregates.

## Quickstart

```sh
dotnet add package SqlLinq.TimeSeries
```

```csharp
using SqlLinq.TimeSeries;

// Time series queries require a timespan selector for their source collection
var rows = readings.TimeSeriesQuery(
    """
    SELECT average(Temperature), max(WindSpeed)
    FROM readings
    FACET Station
    SINCE 1 hour ago
    TIMESERIES 10 minutes
    """,
    r => r.Elapsed);

// Example rows, one per Station facet and 10 minute bucket
// { ["BeginTime"] = 01:00:00, ["EndTime"] = 01:10:00, ["Station"] = "South", ["average(Temperature)"] = 13.5, ["max(WindSpeed)"] = 37 }
// { ["BeginTime"] = 01:00:00, ["EndTime"] = 01:10:00, ["Station"] = "North", ["average(Temperature)"] = 9.5, ["max(WindSpeed)"] = 33 }
// { ["BeginTime"] = 01:10:00, ["EndTime"] = 01:20:00, ["Station"] = "South", ["average(Temperature)"] = 14.2, ["max(WindSpeed)"] = 35 }
```

## Aggregates

| Aggregate    | Example                     |
| ------------ | --------------------------- |
| `median`     | `median(WindSpeed)`         |
| `percentile` | `percentile(WindSpeed, 95)` |
| `latest`     | `latest(Temperature)`       |
| `earliest`   | `earliest(Temperature)`     |
| `rate`       | `rate(count(*), 1 minute)`  |

## Clauses

> `GROUP BY`, `HAVING`, and `ORDER BY` aren't supported for time series data. Use `FACET` to group.

| Clause           | Effect                                                       | Example                                    |
| ---------------- | ------------------------------------------------------------ | ------------------------------------------ |
| `FACET`          | Groups by a value                                            | `FACET Station`                            |
| `TIMESERIES`     | Splits the window into time buckets                          | `TIMESERIES 10 minutes`                    |
| `SINCE`, `UNTIL` | Sets the time window, `ago` counts back from the newest item | `SINCE 1 hour ago UNTIL 10 minutes ago`    |
| `COMPARE WITH`   | Adds an earlier window, marked in a `Comparison` column      | `SINCE 1 hour ago COMPARE WITH 1 hour ago` |
| `LIMIT`          | Keeps the top facets, ranked by the first aggregate          | `FACET Station LIMIT 2`                    |
