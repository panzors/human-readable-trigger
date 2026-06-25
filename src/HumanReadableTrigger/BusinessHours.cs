namespace HumanReadableTrigger;

/// <summary>
/// An inclusive window of wall-clock time within a single day (for example
/// 09:00–17:00), used to confine interval schedules to "business hours".
/// </summary>
public readonly struct BusinessHours
{
    /// <summary>The earliest time of day (inclusive) at which runs may occur.</summary>
    public TimeOnly Start { get; }

    /// <summary>The latest time of day (inclusive) at which runs may occur.</summary>
    public TimeOnly End { get; }

    /// <summary>
    /// Creates a business-hours window.
    /// </summary>
    /// <param name="start">The window's opening time of day.</param>
    /// <param name="end">The window's closing time of day; must be later than <paramref name="start"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="end"/> is not after <paramref name="start"/>.</exception>
    public BusinessHours(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentException($"End time {end} must be after start time {start}.", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>Returns the window in "HH:mm-HH:mm" form.</summary>
    public override string ToString() => $"{Start:HH\\:mm}-{End:HH\\:mm}";
}
