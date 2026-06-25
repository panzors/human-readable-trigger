# HumanReadableTrigger

A .NET class library that parses structured, human-readable schedule text into
a normalized `RunSchedule` for hosted workers.

> **Status:** early prototype.

## Usage

`RunSchedule.Parse` turns a schedule expression into a normalized object that is
both **serializable** (hand it to your hosted service) and able to **compute the
next run(s)** locally. Every schedule is anchored to a concrete wall-clock time
or a fixed cadence — relative expressions like `tomorrow` or `in 2 hours` are
intentionally **not** supported.

```csharp
using HumanReadableTrigger;

var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

var schedule = RunSchedule.Parse("9am every Tuesday", tz);

DateTimeOffset? next = schedule.GetNextRun(DateTimeOffset.UtcNow);
foreach (var run in schedule.GetUpcoming(DateTimeOffset.UtcNow, 5))
    Console.WriteLine(run);

string json = schedule.ToJson();           // persist / send to the worker
var restored = RunSchedule.FromJson(json);  // round-trips losslessly

// Non-throwing variant:
RunSchedule? maybe = RunSchedule.TryParse("every 2 hours", tz);
```

### Supported expressions (case-insensitive)

| Form | Example | Meaning |
|------|---------|---------|
| Weekly | `9am every Tuesday`, `5pm every Monday and Thursday`, `9am daily` | a time of day on one or more weekdays |
| Monthly | `5pm every 15th` | a time of day on a day of the month (clamped for short months) |
| Interval | `every 2 hours`, `every 30 minutes` | a fixed cadence that starts immediately |
| Interval (bounded) | `every hour between 9am and 1600 Monday Tuesday Thursday` | a cadence confined to business hours and weekdays |
| Continuous sleep | `continuous sleep 12min`, `sleep 12 minutes between runs` | run, sleep a fixed gap, repeat |
| Cron (fallback) | `cron: 0 9 * * 2` | a raw cron expression — stored and validated, **not** evaluated by this library |

Times accept `9am`, `5pm`, `1600`, `16:00`, `noon` and `midnight`. Durations
accept forms like `12min`, `2 hours` and `90m` (seconds, minutes, hours, days,
weeks, plus common abbreviations).

### Notes on semantics

- **Interval** anchors: a bare `every 2 hours` starts immediately and repeats on
  that cadence; a bounded `every hour between 9am and 4pm` fires on the hour
  within the window and rolls to the next allowed day.
- **`GetNextRun(after)`** returns the next run after `after`. For `cron` it
  returns `null` (your hosted service evaluates cron itself).
- All wall-clock fields are interpreted in the supplied `TimeZoneInfo`
  (default: local), and the resolved `DateTimeOffset` carries the correct
  (DST-aware) offset.

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
