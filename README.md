# HumanReadableTrigger

A .NET class library for parsing human-readable trigger expressions.

> **Status:** early prototype. The first piece — the `TriggerTime` parser — is
> now implemented; more trigger types may follow.

## Usage

`TriggerTime` turns a human-readable expression into a concrete
`DateTimeOffset`. Expressions without their own time-zone information are
interpreted in the supplied time-zone override (or the local zone when none is
given), so results are deterministic regardless of where the code runs.

```csharp
using HumanReadableTrigger;

var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

var when = new TriggerTime("tomorrow at 9am", tz);
Console.WriteLine(when.Value); // the resolved instant in the override zone
Console.WriteLine(when.Utc);   // the same instant in UTC

// Non-throwing variant:
TriggerTime? maybe = TriggerTime.TryParse("in 30 minutes", tz);
```

Supported expression forms (case-insensitive):

- `now`
- `in <n> <unit> [<n> <unit> ...]` — e.g. `in 1 hour 30 minutes`
- `<n> <unit> ago` — e.g. `2 hours ago`
- `today | tomorrow | yesterday [at] <time>`
- `[next] <weekday> [at] <time>`
- a bare time of day such as `9am`, `14:30` or `noon` (resolved to today)
- an absolute date/time such as `2026-12-25 08:00` or `2026-12-25T08:00:00Z`

Time units understood: seconds, minutes, hours, days and weeks (plus common
abbreviations like `min`, `hr`, `d`, `wk`).

To make relative expressions deterministic (for example in tests), pass an
explicit reference instant:

```csharp
var reference = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
var when = new TriggerTime("in 5 minutes", tz, reference);
```

## Target frameworks

The library multi-targets all currently supported .NET **LTS** releases:

- `net8.0`
- `net10.0`

## Building and testing

```sh
dotnet build
dotnet test
```

## Packaging

NuGet packaging is configured but **disabled while prototyping**. To re-enable,
set `GeneratePackageOnBuild` back to `true` in
`src/HumanReadableTrigger/HumanReadableTrigger.csproj` (and re-enable the pack
step in `.github/workflows/ci.yml`), then:

```sh
dotnet pack -c Release
```
