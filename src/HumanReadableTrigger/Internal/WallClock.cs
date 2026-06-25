namespace HumanReadableTrigger;

/// <summary>
/// Builds zone-correct <see cref="DateTimeOffset"/> values from wall-clock times.
/// </summary>
internal static class WallClock
{
    /// <summary>
    /// Interprets <paramref name="local"/> as a wall-clock time in <paramref name="tz"/>
    /// and returns the corresponding <see cref="DateTimeOffset"/>, using the correct
    /// UTC offset (including daylight-saving adjustments) for that instant.
    /// </summary>
    public static DateTimeOffset FromWallClock(DateTime local, TimeZoneInfo tz)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }
}
