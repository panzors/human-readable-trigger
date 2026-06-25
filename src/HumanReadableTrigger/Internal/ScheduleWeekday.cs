namespace HumanReadableTrigger;

/// <summary>
/// Parses weekday names and abbreviations into <see cref="DayOfWeek"/> values.
/// </summary>
internal static class ScheduleWeekday
{
    public static bool TryParse(string token, out DayOfWeek day)
    {
        switch (token.Trim().TrimEnd(',').ToLowerInvariant())
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
}
