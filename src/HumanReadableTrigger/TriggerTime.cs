using System.Globalization;
using System.Text.RegularExpressions;

namespace HumanReadableTrigger;

/// <summary>
/// Parses a human-readable trigger expression (for example <c>"now"</c>,
/// <c>"in 30 minutes"</c>, <c>"tomorrow at 9am"</c> or <c>"next friday at 18:30"</c>)
/// into a concrete <see cref="DateTimeOffset"/>.
/// </summary>
/// <remarks>
/// <para>
/// Expressions that do not carry their own time-zone information are interpreted
/// in the supplied <em>time-zone override</em> (or <see cref="TimeZoneInfo.Local"/>
/// when no override is given). This makes the result deterministic regardless of
/// the machine the code happens to run on.
/// </para>
/// <para>Supported forms (case-insensitive):</para>
/// <list type="bullet">
///   <item><description><c>now</c></description></item>
///   <item><description><c>in &lt;n&gt; &lt;unit&gt; [&lt;n&gt; &lt;unit&gt; ...]</c> — e.g. <c>in 1 hour 30 minutes</c></description></item>
///   <item><description><c>&lt;n&gt; &lt;unit&gt; ago</c> — e.g. <c>2 hours ago</c></description></item>
///   <item><description><c>today|tomorrow|yesterday [at] &lt;time&gt;</c></description></item>
///   <item><description><c>[next] &lt;weekday&gt; [at] &lt;time&gt;</c></description></item>
///   <item><description>a bare time of day — e.g. <c>9am</c>, <c>14:30</c>, <c>noon</c> (resolved to today)</description></item>
///   <item><description>an absolute date/time — e.g. <c>2026-12-25 08:00</c> or <c>2026-12-25T08:00:00Z</c></description></item>
/// </list>
/// <para>
/// Time units understood are seconds, minutes, hours, days and weeks (including
/// common abbreviations such as <c>min</c>, <c>hr</c>, <c>d</c>, <c>wk</c>).
/// </para>
/// </remarks>
public sealed class TriggerTime
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Matches a trailing explicit UTC offset or 'Z' designator, e.g. "Z", "+05:00", "-0800".
    private static readonly Regex ExplicitOffsetRegex =
        new(@"([zZ]|[+-]\d{2}:?\d{2})$", RegexOptions.Compiled);

    /// <summary>The original expression supplied to the constructor.</summary>
    public string Expression { get; }

    /// <summary>The time zone the expression was interpreted in.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>The resolved trigger instant, expressed in <see cref="TimeZone"/>.</summary>
    public DateTimeOffset Value { get; }

    /// <summary>The resolved trigger instant in UTC.</summary>
    public DateTimeOffset Utc => Value.ToUniversalTime();

    /// <summary>
    /// Parses <paramref name="expression"/> relative to the current moment.
    /// </summary>
    /// <param name="expression">The human-readable trigger expression.</param>
    /// <param name="timeZoneOverride">
    /// The time zone used to interpret expressions without explicit zone
    /// information. Defaults to <see cref="TimeZoneInfo.Local"/> when <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The expression could not be parsed.</exception>
    public TriggerTime(string expression, TimeZoneInfo? timeZoneOverride = null)
        : this(expression, timeZoneOverride, DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Parses <paramref name="expression"/> relative to a caller-supplied reference
    /// instant. This overload makes relative expressions deterministic and testable.
    /// </summary>
    /// <param name="expression">The human-readable trigger expression.</param>
    /// <param name="timeZoneOverride">
    /// The time zone used to interpret expressions without explicit zone
    /// information. Defaults to <see cref="TimeZoneInfo.Local"/> when <see langword="null"/>.
    /// </param>
    /// <param name="reference">The instant that relative expressions (e.g. "now", "in 5 minutes") are measured from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The expression could not be parsed.</exception>
    public TriggerTime(string expression, TimeZoneInfo? timeZoneOverride, DateTimeOffset reference)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        TimeZone = timeZoneOverride ?? TimeZoneInfo.Local;
        Value = Evaluate(expression, TimeZone, reference);
    }

    /// <summary>
    /// Parses <paramref name="expression"/>, returning <see langword="null"/>
    /// instead of throwing when it cannot be parsed.
    /// </summary>
    public static TriggerTime? TryParse(string expression, TimeZoneInfo? timeZoneOverride = null)
        => TryParse(expression, timeZoneOverride, DateTimeOffset.UtcNow);

    /// <summary>
    /// Parses <paramref name="expression"/> relative to <paramref name="reference"/>,
    /// returning <see langword="null"/> instead of throwing when it cannot be parsed.
    /// </summary>
    public static TriggerTime? TryParse(string expression, TimeZoneInfo? timeZoneOverride, DateTimeOffset reference)
    {
        if (expression is null)
        {
            return null;
        }

        try
        {
            return new TriggerTime(expression, timeZoneOverride, reference);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Returns the resolved instant in round-trip ("O") format.</summary>
    public override string ToString() => Value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Evaluate(string expression, TimeZoneInfo tz, DateTimeOffset reference)
    {
        var raw = expression.Trim();
        if (raw.Length == 0)
        {
            throw new FormatException("Trigger expression is empty.");
        }

        var text = WhitespaceRegex.Replace(raw, " ").ToLowerInvariant();

        // The reference moment expressed as wall-clock time in the target zone.
        var nowLocal = TimeZoneInfo.ConvertTime(reference, tz);

        if (text == "now")
        {
            return TimeZoneInfo.ConvertTime(reference, tz);
        }

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // "in <n> <unit> ..."
        if (tokens.Length > 1 && tokens[0] == "in")
        {
            var delta = ParseDuration(tokens, 1, tokens.Length);
            return TimeZoneInfo.ConvertTime(reference + delta, tz);
        }

        // "<n> <unit> ... ago"
        if (tokens.Length > 1 && tokens[^1] == "ago")
        {
            var delta = ParseDuration(tokens, 0, tokens.Length - 1);
            return TimeZoneInfo.ConvertTime(reference - delta, tz);
        }

        // today / tomorrow / yesterday / [next] weekday / bare time-of-day
        if (TryParseDayExpression(tokens, nowLocal.DateTime, tz, out var dayResult))
        {
            return dayResult;
        }

        // Absolute value that carries its own zone information.
        if (ExplicitOffsetRegex.IsMatch(raw)
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            return TimeZoneInfo.ConvertTime(dto, tz);
        }

        // Absolute value without zone information -> interpret as wall-clock in the target zone.
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtLocal))
        {
            return FromWallClock(dtLocal, tz);
        }

        throw new FormatException($"Unrecognized trigger expression: '{expression}'.");
    }

    private static bool TryParseDayExpression(
        IReadOnlyList<string> tokens, DateTime nowLocal, TimeZoneInfo tz, out DateTimeOffset result)
    {
        result = default;

        DateTime baseDate = nowLocal.Date;
        bool haveDate = false;
        int index = 0;

        if (index < tokens.Count)
        {
            var token = tokens[index];
            if (token == "today")
            {
                haveDate = true;
                index++;
            }
            else if (token == "tomorrow")
            {
                baseDate = baseDate.AddDays(1);
                haveDate = true;
                index++;
            }
            else if (token == "yesterday")
            {
                baseDate = baseDate.AddDays(-1);
                haveDate = true;
                index++;
            }
            else if (token == "next" && index + 1 < tokens.Count && TryParseWeekday(tokens[index + 1], out var nextDay))
            {
                baseDate = ResolveWeekday(baseDate, nextDay, strictlyAfter: true);
                haveDate = true;
                index += 2;
            }
            else if (TryParseWeekday(token, out var weekday))
            {
                baseDate = ResolveWeekday(baseDate, weekday, strictlyAfter: false);
                haveDate = true;
                index++;
            }
        }

        // An optional "at" separator between the date part and the time part.
        if (index < tokens.Count && tokens[index] == "at")
        {
            index++;
        }

        TimeSpan timeOfDay;
        if (index < tokens.Count)
        {
            // Join the remainder and strip spaces so "9 am" and "9am" parse identically.
            var timeText = string.Concat(tokens.Skip(index));
            if (!TryParseTimeOfDay(timeText, out timeOfDay))
            {
                return false;
            }
        }
        else if (haveDate)
        {
            // A date with no time component resolves to the start of that day.
            timeOfDay = TimeSpan.Zero;
        }
        else
        {
            return false;
        }

        result = FromWallClock(baseDate + timeOfDay, tz);
        return true;
    }

    private static TimeSpan ParseDuration(IReadOnlyList<string> tokens, int start, int end)
    {
        var count = end - start;
        if (count < 2 || count % 2 != 0)
        {
            throw new FormatException("Expected one or more '<number> <unit>' pairs in the duration.");
        }

        var total = TimeSpan.Zero;
        for (int i = start; i < end; i += 2)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            {
                throw new FormatException($"Expected a number but found '{tokens[i]}'.");
            }

            total += UnitToTimeSpan(tokens[i + 1], amount)
                ?? throw new FormatException($"Unknown time unit: '{tokens[i + 1]}'.");
        }

        return total;
    }

    private static TimeSpan? UnitToTimeSpan(string unit, double amount) => unit switch
    {
        "second" or "seconds" or "sec" or "secs" or "s" => TimeSpan.FromSeconds(amount),
        "minute" or "minutes" or "min" or "mins" or "m" => TimeSpan.FromMinutes(amount),
        "hour" or "hours" or "hr" or "hrs" or "h" => TimeSpan.FromHours(amount),
        "day" or "days" or "d" => TimeSpan.FromDays(amount),
        "week" or "weeks" or "wk" or "wks" or "w" => TimeSpan.FromDays(amount * 7),
        _ => null,
    };

    private static bool TryParseWeekday(string token, out DayOfWeek day)
    {
        switch (token)
        {
            case "sunday" or "sun": day = DayOfWeek.Sunday; return true;
            case "monday" or "mon": day = DayOfWeek.Monday; return true;
            case "tuesday" or "tue" or "tues": day = DayOfWeek.Tuesday; return true;
            case "wednesday" or "wed": day = DayOfWeek.Wednesday; return true;
            case "thursday" or "thu" or "thur" or "thurs": day = DayOfWeek.Thursday; return true;
            case "friday" or "fri": day = DayOfWeek.Friday; return true;
            case "saturday" or "sat": day = DayOfWeek.Saturday; return true;
            default: day = default; return false;
        }
    }

    private static DateTime ResolveWeekday(DateTime today, DayOfWeek target, bool strictlyAfter)
    {
        int diff = ((int)target - (int)today.DayOfWeek + 7) % 7;
        if (diff == 0 && strictlyAfter)
        {
            diff = 7;
        }

        return today.AddDays(diff);
    }

    private static bool TryParseTimeOfDay(string text, out TimeSpan time)
    {
        time = default;
        text = text.Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            return false;
        }

        switch (text)
        {
            case "midnight":
                time = TimeSpan.Zero;
                return true;
            case "noon" or "midday":
                time = TimeSpan.FromHours(12);
                return true;
        }

        bool hasMeridiem = false;
        bool isPm = false;
        if (text.EndsWith("am", StringComparison.Ordinal))
        {
            hasMeridiem = true;
            text = text[..^2].Trim();
        }
        else if (text.EndsWith("pm", StringComparison.Ordinal))
        {
            hasMeridiem = true;
            isPm = true;
            text = text[..^2].Trim();
        }

        var parts = text.Split(':');
        if (parts.Length > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours))
        {
            return false;
        }

        int minutes = 0, seconds = 0;
        if (parts.Length >= 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
        {
            return false;
        }

        if (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out seconds))
        {
            return false;
        }

        if (hasMeridiem)
        {
            if (hours is < 1 or > 12)
            {
                return false;
            }

            if (isPm && hours != 12)
            {
                hours += 12;
            }
            else if (!isPm && hours == 12)
            {
                hours = 0;
            }
        }

        if (hours is < 0 or > 23 || minutes is < 0 or > 59 || seconds is < 0 or > 59)
        {
            return false;
        }

        time = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    /// <summary>
    /// Builds a <see cref="DateTimeOffset"/> from a wall-clock <paramref name="local"/>
    /// time interpreted in <paramref name="tz"/>, using the correct UTC offset
    /// (including daylight-saving adjustments) for that instant.
    /// </summary>
    private static DateTimeOffset FromWallClock(DateTime local, TimeZoneInfo tz)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }
}
