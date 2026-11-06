[![CI](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml/badge.svg)](https://github.com/lennykean/SqlLinq/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqlLinq.Units)](https://www.nuget.org/packages/SqlLinq.Units)

# Units extension for SqlLinq

[SqlLinq](https://github.com/lennykean/SqlLinq) allows using ANSI style SQL `SELECT` statements over collections.

`SqlLinq.Units` extends basic SQL queries with unit conversion.

## Quickstart

```sh
dotnet add package SqlLinq.Units
```

```csharp
using System.Globalization;
using SqlLinq.Units;
using UnitsNet.Metadata;
using UnitsNet.Metadata.Annotations;
using UnitsNet.Units;

public class Reading
{
    // Units are declared with UnitsNet.Metadata attributes
    [Quantity(TemperatureUnit.DegreeCelsius)]
    public double Temperature { get; set; }
}

// Conversions are built from the type's unit metadata
var units = new UnitConversions(new Reading().GetObjectMetadata(CultureInfo.InvariantCulture).Values);

// convert() reads Temperature in °F, though it's declared in °C
var warm = readings.UnitQuery("SELECT * FROM this WHERE convert(Temperature, '°F') > 60", units);
```

## Conversions

Units are written as abbreviations, such as `°F` or `psi`. Only linear conversions are supported.

| Form                                | Converts                             | Example                                     |
| ----------------------------------- | ------------------------------------ | ------------------------------------------- |
| `convert(column, 'to')`             | A property with a declared unit      | `convert(Temperature, 'K')`                 |
| `convert(column, 'from', 'to')`     | The same, naming the property's unit | `convert(Temperature, '°C', '°F')`          |
| `convert(value, 'Kind.from', 'to')` | Any value                            | `convert(Pressure, 'Pressure.hPa', 'inHg')` |
