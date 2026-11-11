[![CI](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml/badge.svg)](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqlLinq.TimeSeries)](https://www.nuget.org/packages/SqlLinq.TimeSeries)

# Time series extension for SqlLinq

[SqlLinq](https://github.com/lennykean/SqlLinq) allows using ANSI style SQL `SELECT` statements over collections.

`SqlLinq.TimeSeries` extends basic SQL queries with time series clauses and aggregates.

```sh
dotnet add package SqlLinq.TimeSeries
```

## Time series dialect extensions

> `GROUP BY`, `HAVING`, and `ORDER BY` aren't supported for time series data. Use `FACET` to group.

### Clauses

- **`SINCE`, `UNTIL`** set the time window to query. A plain duration is a fixed time, and `ago` counts back from the newest item: `SINCE 1 hour ago UNTIL 10 minutes ago`.
- **`FACET`** groups by a value and returns a row per group: `FACET Station`.
- **`LIMIT`** keeps the top facets, ranked by the first aggregate: `FACET Station LIMIT 2`.
- **`TIMESERIES`** splits the window into buckets of a fixed size and returns a row per bucket, including empty ones: `TIMESERIES 10 minutes`.
- **`SESSION`** splits the items into sessions and returns a row per session. A session ends when the next item is further away than the gap, so each row is one burst of activity and quiet periods return nothing: `SESSION 1 minute`. It can't be combined with `TIMESERIES`.
- **`COMPARE WITH`** runs the query again over an earlier window and marks each row `current` or `previous` in a `Comparison` column: `SINCE 1 hour ago COMPARE WITH 1 hour ago`.

### Aggregates

- **`median`** is the middle value: `median(WindSpeed)`.
- **`percentile`** is the value at a percent of the values: `percentile(WindSpeed, 95)`.
- **`latest`, `earliest`** are the values of the newest and oldest items: `latest(Temperature)`.
- **`rate`** scales an aggregate to a duration, for how often something happens: `rate(count(*), 1 minute)`.
- **`derivative`** is the slope of a line fitted through the values, for how fast a value changes: `derivative(Pressure, 1 hour)`.

## Examples

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

```csharp
// WHERE says what counts as a gust, and SESSION returns a row per gust:
// readings over 30 that are less than a minute apart are one gust
var gusts = readings.TimeSeriesQuery(
    """
    SELECT max(WindSpeed)
    FROM readings
    WHERE WindSpeed > 30
    SESSION 1 minute
    """,
    r => r.Elapsed);

// { ["BeginTime"] = 01:02:00, ["EndTime"] = 01:04:30, ["max(WindSpeed)"] = 37 }
// { ["BeginTime"] = 01:12:00, ["EndTime"] = 01:13:00, ["max(WindSpeed)"] = 35 }
```
