using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HumanReadableTrigger;

/// <summary>
/// A normalized, serializable description of a hosted-worker run schedule,
/// parsed from a human-readable expression. Every schedule is anchored to a
/// concrete wall-clock time (or a fixed cadence); relative expressions such as
/// "tomorrow" or "in 2 hours" are intentionally not supported.
/// </summary>
/// <remarks>
/// <para>Supported expressions (case-insensitive):</para>
/// <list type="bullet">
///   <item><description><c>9am every Tuesday</c> — a time of day on one or more weekdays (<see cref="ScheduleKind.Weekly"/>).</description></item>
///   <item><description><c>5pm every 15th</c> — a time of day on a day of the month (<see cref="ScheduleKind.Monthly"/>).</description></item>
///   <item><description><c>every 2 hours</c> — a fixed cadence that starts immediately (<see cref="ScheduleKind.Interval"/>).</description></item>
///   <item><description><c>every hour between 9am and 1600 Monday Tuesday Thursday</c> — a cadence confined to a window and weekdays.</description></item>
///   <item><description><c>continuous sleep 12min</c> — run, then sleep a fixed gap, repeat (<see cref="ScheduleKind.ContinuousSleep"/>).</description></item>
///   <item><description><c>cron: 0 9 * * 2</c> — a raw cron expression, stored and validated but not evaluated (<see cref="ScheduleKind.Cron"/>).</description></item>
/// </list>
/// </remarks>
public sealed class RunSchedule
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly DayOfWeek[] AllDays =
    {
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The category of this schedule.</summary>
    public ScheduleKind Kind { get; set; }

    /// <summary>The time zone all wall-clock fields are interpreted in.</summary>
    [JsonIgnore]
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

    /// <summary>
    /// The identifier of <see cref="TimeZone"/>. This is the serialized form of the
    /// zone; setting it resolves <see cref="TimeZone"/> via <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>.
    /// </summary>
    public string TimeZoneId
    {
        get => TimeZone.Id;
        set => TimeZone = TimeZoneInfo.FindSystemTimeZoneById(value);
    }

    /// <summary>The time of day a run fires (for <see cref="ScheduleKind.Weekly"/> and <see cref="ScheduleKind.Monthly"/>).</summary>
    public TimeOnly? TimeOfDay { get; set; }

    /// <summary>
    /// The days a run may fire on. Required for <see cref="ScheduleKind.Weekly"/>; optional
    /// for <see cref="ScheduleKind.Interval"/> (when set, the cadence is confined to these days).
    /// </summary>
    public DayOfWeek[]? DaysOfWeek { get; set; }

    /// <summary>The day of the month (1–31) a run fires on, for <see cref="ScheduleKind.Monthly"/>. Days beyond a month's length are clamped to its last day.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>The cadence between runs, for <see cref="ScheduleKind.Interval"/>.</summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>The gap slept between runs, for <see cref="ScheduleKind.ContinuousSleep"/>.</summary>
    public TimeSpan? SleepBetween { get; set; }

    /// <summary>An optional business-hours window that confines an <see cref="ScheduleKind.Interval"/> schedule.</summary>
    public BusinessHours? Window { get; set; }

    /// <summary>The raw cron expression, for <see cref="ScheduleKind.Cron"/>. This library does not evaluate it.</summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Parses <paramref name="text"/> into a <see cref="RunSchedule"/>.
    /// </summary>
    /// <param name="text">The human-readable schedule expression.</param>
    /// <param name="timeZone">The zone wall-clock fields are interpreted in. Defaults to <see cref="TimeZoneInfo.Local"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The expression could not be parsed.</exception>
    public static RunSchedule Parse(string text, TimeZoneInfo? timeZone = null)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var tz = timeZone ?? TimeZoneInfo.Local;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            throw new FormatException("Schedule expression is empty.");
        }

        var normalized = WhitespaceRegex.Replace(trimmed, " ").ToLowerInvariant();

        if (normalized.StartsWith("cron", StringComparison.Ordinal))
        {
            return ParseCron(trimmed, tz);
        }

        if (normalized.StartsWith("continuous sleep", StringComparison.Ordinal)
            || normalized.StartsWith("sleep", StringComparison.Ordinal))
        {
            return ParseSleep(normalized, tz, text);
        }

        if (normalized.StartsWith("every ", StringComparison.Ordinal))
        {
            return ParseInterval(normalized["every ".Length..], tz, text);
        }

        return ParseTimed(normalized, tz, text);
    }

    /// <summary>
    /// Parses <paramref name="text"/>, returning <see langword="null"/> instead of
    /// throwing when it cannot be parsed.
    /// </summary>
    public static RunSchedule? TryParse(string text, TimeZoneInfo? timeZone = null)
    {
        if (text is null)
        {
            return null;
        }

        try
        {
            return Parse(text, timeZone);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            // Covers invalid windows (BusinessHours) and unknown zone ids.
            return null;
        }
    }

    /// <summary>
    /// Computes the next run strictly after <paramref name="after"/>. For
    /// <see cref="ScheduleKind.Cron"/> this returns <see langword="null"/> (the hosted
    /// service is expected to evaluate cron itself).
    /// </summary>
    public DateTimeOffset? GetNextRun(DateTimeOffset after) => Kind switch
    {
        ScheduleKind.Weekly => NextWeekly(after),
        ScheduleKind.Monthly => NextMonthly(after),
        ScheduleKind.Interval => NextInterval(after),
        ScheduleKind.ContinuousSleep => after + SleepBetween!.Value,
        _ => null,
    };

    /// <summary>
    /// Enumerates up to <paramref name="count"/> upcoming runs after <paramref name="after"/>.
    /// Yields nothing for schedules whose next run cannot be computed (e.g. cron).
    /// </summary>
    public IEnumerable<DateTimeOffset> GetUpcoming(DateTimeOffset after, int count)
    {
        var cursor = after;
        for (int i = 0; i < count; i++)
        {
            var next = GetNextRun(cursor);
            if (next is null)
            {
                yield break;
            }

            yield return next.Value;
            cursor = next.Value;
        }
    }

    /// <summary>Serializes this schedule to JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Deserializes a schedule previously produced by <see cref="ToJson"/>.</summary>
    /// <exception cref="FormatException">The JSON did not represent a schedule.</exception>
    public static RunSchedule FromJson(string json)
        => JsonSerializer.Deserialize<RunSchedule>(json, JsonOptions)
           ?? throw new FormatException("Invalid schedule JSON.");

    /// <summary>Returns the schedule's kind and key parameters for debugging.</summary>
    public override string ToString() => Kind switch
    {
        ScheduleKind.Weekly => $"Weekly {TimeOfDay} on {string.Join(",", DaysOfWeek ?? AllDays)}",
        ScheduleKind.Monthly => $"Monthly {TimeOfDay} on day {DayOfMonth}",
        ScheduleKind.Interval => $"Interval {Interval}{(Window is { } w ? $" {w}" : "")}",
        ScheduleKind.ContinuousSleep => $"ContinuousSleep {SleepBetween}",
        ScheduleKind.Cron => $"Cron {CronExpression}",
        _ => Kind.ToString(),
    };

    // ---- Parsing ----------------------------------------------------------

    private static RunSchedule ParseCron(string trimmed, TimeZoneInfo tz)
    {
        var rest = trimmed["cron".Length..].TrimStart();
        if (rest.StartsWith(':'))
        {
            rest = rest[1..].TrimStart();
        }

        var fields = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is < 5 or > 6)
        {
            throw new FormatException($"A cron expression must have 5 or 6 fields, found {fields.Length}.");
        }

        return new RunSchedule
        {
            Kind = ScheduleKind.Cron,
            TimeZone = tz,
            CronExpression = string.Join(' ', fields),
        };
    }

    private static RunSchedule ParseSleep(string normalized, TimeZoneInfo tz, string original)
    {
        var rest = normalized.StartsWith("continuous sleep", StringComparison.Ordinal)
            ? normalized["continuous sleep".Length..]
            : normalized["sleep".Length..];

        rest = rest.Replace("between runs", " ").Trim();
        if (rest.StartsWith("for ", StringComparison.Ordinal))
        {
            rest = rest["for ".Length..];
        }

        if (!HumanDuration.TryParse(rest, out var gap) || gap <= TimeSpan.Zero)
        {
            throw new FormatException($"Could not parse a sleep duration from '{original}'.");
        }

        return new RunSchedule
        {
            Kind = ScheduleKind.ContinuousSleep,
            TimeZone = tz,
            SleepBetween = gap,
        };
    }

    private static RunSchedule ParseInterval(string rest, TimeZoneInfo tz, string original)
    {
        var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Pull any trailing weekday names off the end.
        var days = new List<DayOfWeek>();
        while (tokens.Count > 0)
        {
            var last = tokens[^1];
            if (last is "and" or "&" or ",")
            {
                tokens.RemoveAt(tokens.Count - 1);
                continue;
            }

            if (ScheduleWeekday.TryParse(last, out var day))
            {
                if (!days.Contains(day))
                {
                    days.Insert(0, day);
                }

                tokens.RemoveAt(tokens.Count - 1);
            }
            else
            {
                break;
            }
        }

        // Pull an optional "between <time> and <time>" window.
        BusinessHours? window = null;
        int betweenIndex = tokens.IndexOf("between");
        if (betweenIndex >= 0)
        {
            int andIndex = tokens.IndexOf("and", betweenIndex + 1);
            if (andIndex < 0 || andIndex + 1 >= tokens.Count)
            {
                throw new FormatException($"Could not parse a 'between … and …' window from '{original}'.");
            }

            var startText = string.Concat(tokens.GetRange(betweenIndex + 1, andIndex - betweenIndex - 1));
            var endText = string.Concat(tokens.GetRange(andIndex + 1, tokens.Count - andIndex - 1));
            if (!TimeOfDayParser.TryParse(startText, out var windowStart)
                || !TimeOfDayParser.TryParse(endText, out var windowEnd))
            {
                throw new FormatException($"Could not parse the window times in '{original}'.");
            }

            window = new BusinessHours(windowStart, windowEnd);
            tokens.RemoveRange(betweenIndex, tokens.Count - betweenIndex);
        }

        // Whatever remains is the interval. Allow a bare unit ("hour") to mean "1 hour".
        var intervalText = string.Join(' ', tokens).Trim();
        if (intervalText.Length == 0)
        {
            throw new FormatException($"Could not find an interval in '{original}'.");
        }

        if (!char.IsDigit(intervalText[0]))
        {
            intervalText = "1 " + intervalText;
        }

        if (!HumanDuration.TryParse(intervalText, out var interval) || interval <= TimeSpan.Zero)
        {
            throw new FormatException($"Could not parse an interval from '{original}'.");
        }

        return new RunSchedule
        {
            Kind = ScheduleKind.Interval,
            TimeZone = tz,
            Interval = interval,
            Window = window,
            DaysOfWeek = days.Count > 0 ? days.OrderBy(d => d).ToArray() : null,
        };
    }

    private static RunSchedule ParseTimed(string normalized, TimeZoneInfo tz, string original)
    {
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            throw new FormatException($"Unrecognized schedule expression: '{original}'.");
        }

        // Allow the time to be a single token ("9am") or two ("9 am").
        var timeText = words[0];
        int index = 1;
        if (words.Length > 1 && words[1] is "am" or "pm")
        {
            timeText += words[1];
            index = 2;
        }

        if (!TimeOfDayParser.TryParse(timeText, out var timeOfDay))
        {
            throw new FormatException($"Unrecognized schedule expression: '{original}'.");
        }

        var rest = string.Join(' ', words.Skip(index)).Trim();
        if (rest.StartsWith("every ", StringComparison.Ordinal))
        {
            rest = rest["every ".Length..];
        }
        else if (rest == "every")
        {
            rest = "";
        }
        else if (rest.StartsWith("on ", StringComparison.Ordinal))
        {
            rest = rest["on ".Length..];
        }

        rest = rest.Trim();

        // No qualifier, or an explicit "day"/"daily" => every day at the time.
        if (rest is "" or "day" or "days" or "daily")
        {
            return new RunSchedule
            {
                Kind = ScheduleKind.Weekly,
                TimeZone = tz,
                TimeOfDay = timeOfDay,
                DaysOfWeek = AllDays,
            };
        }

        var restTokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // A leading number/ordinal => day of the month.
        if (TryParseDayOfMonth(restTokens[0], out var dayOfMonth))
        {
            return new RunSchedule
            {
                Kind = ScheduleKind.Monthly,
                TimeZone = tz,
                TimeOfDay = timeOfDay,
                DayOfMonth = dayOfMonth,
            };
        }

        // Otherwise a list of weekdays.
        var days = ParseWeekdayList(restTokens, original);
        return new RunSchedule
        {
            Kind = ScheduleKind.Weekly,
            TimeZone = tz,
            TimeOfDay = timeOfDay,
            DaysOfWeek = days.OrderBy(d => d).ToArray(),
        };
    }

    private static bool TryParseDayOfMonth(string token, out int day)
    {
        day = 0;
        var text = token.Trim().TrimEnd('.');
        foreach (var suffix in new[] { "st", "nd", "rd", "th" })
        {
            if (text.EndsWith(suffix, StringComparison.Ordinal))
            {
                text = text[..^2];
                break;
            }
        }

        return int.TryParse(text, out day) && day is >= 1 and <= 31;
    }

    private static List<DayOfWeek> ParseWeekdayList(IEnumerable<string> tokens, string original)
    {
        var days = new List<DayOfWeek>();
        foreach (var raw in tokens)
        {
            var token = raw.Trim().TrimEnd(',');
            if (token is "" or "and" or "&")
            {
                continue;
            }

            if (!ScheduleWeekday.TryParse(token, out var day))
            {
                throw new FormatException($"Unrecognized day '{raw}' in '{original}'.");
            }

            if (!days.Contains(day))
            {
                days.Add(day);
            }
        }

        if (days.Count == 0)
        {
            throw new FormatException($"No days found in '{original}'.");
        }

        return days;
    }

    // ---- Computation ------------------------------------------------------

    private DateTimeOffset NextWeekly(DateTimeOffset after)
    {
        var days = DaysOfWeek is { Length: > 0 } ? DaysOfWeek : AllDays;
        var time = TimeOfDay!.Value.ToTimeSpan();
        var local = TimeZoneInfo.ConvertTime(after, TimeZone);

        for (int i = 0; i <= 7; i++)
        {
            var date = local.Date.AddDays(i);
            if (Array.IndexOf(days, date.DayOfWeek) < 0)
            {
                continue;
            }

            var candidate = WallClock.FromWallClock(date + time, TimeZone);
            if (candidate.UtcDateTime > after.UtcDateTime)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No upcoming weekly run could be found.");
    }

    private DateTimeOffset NextMonthly(DateTimeOffset after)
    {
        var time = TimeOfDay!.Value.ToTimeSpan();
        var local = TimeZoneInfo.ConvertTime(after, TimeZone);
        int year = local.Year;
        int month = local.Month;

        for (int i = 0; i <= 12; i++)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int day = Math.Min(DayOfMonth!.Value, daysInMonth);
            var candidate = WallClock.FromWallClock(new DateTime(year, month, day) + time, TimeZone);
            if (candidate.UtcDateTime > after.UtcDateTime)
            {
                return candidate;
            }

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        throw new InvalidOperationException("No upcoming monthly run could be found.");
    }

    private DateTimeOffset? NextInterval(DateTimeOffset after)
    {
        var interval = Interval!.Value;

        // Unbounded cadence: start immediately, then every interval.
        if (Window is null && DaysOfWeek is null)
        {
            return after + interval;
        }

        var start = Window?.Start.ToTimeSpan() ?? TimeSpan.Zero;
        var endInclusive = Window?.End.ToTimeSpan() ?? (TimeSpan.FromDays(1) - TimeSpan.FromTicks(1));
        var local = TimeZoneInfo.ConvertTime(after, TimeZone);

        for (int i = 0; i <= 7; i++)
        {
            var date = local.Date.AddDays(i);
            if (DaysOfWeek is { } days && Array.IndexOf(days, date.DayOfWeek) < 0)
            {
                continue;
            }

            for (var t = start; t <= endInclusive; t += interval)
            {
                var candidate = WallClock.FromWallClock(date + t, TimeZone);
                if (candidate.UtcDateTime > after.UtcDateTime)
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
