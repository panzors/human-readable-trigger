# HumanReadableTrigger

A small .NET library that parses human-readable trigger expressions such as
`"every 5 minutes"` or `"every 2 hours"` into a structured, strongly-typed
interval.

## Installation

```sh
dotnet add package HumanReadableTrigger
```

## Usage

```csharp
using HumanReadableTrigger;

TriggerInterval interval = TriggerParser.Parse("every 5 minutes");

Console.WriteLine(interval.Count);          // 5
Console.WriteLine(interval.Unit);           // Minute
Console.WriteLine(interval.ToTimeSpan());   // 00:05:00

// Non-throwing variant:
if (TriggerParser.TryParse("every 2 hours", out TriggerInterval every2Hours))
{
    // every2Hours.ToTimeSpan() == TimeSpan.FromHours(2)
}
```

Supported expression forms (case- and whitespace-insensitive):

- `every <count> <unit>` &mdash; e.g. `every 30 seconds`
- `every <unit>` &mdash; count defaults to `1`, e.g. `every hour`
- `<count> <unit>` &mdash; the leading `every` is optional, e.g. `5 minutes`

Units: `second(s)`, `minute(s)`, `hour(s)`, `day(s)`.

## Target frameworks

The library multi-targets all currently supported .NET **LTS** releases:

- `net8.0`
- `net10.0`

## Building and testing

```sh
dotnet build
dotnet test
```

Building also produces a NuGet package under `artifacts/packages/`
(`GeneratePackageOnBuild` is enabled). To pack explicitly:

```sh
dotnet pack -c Release
```
