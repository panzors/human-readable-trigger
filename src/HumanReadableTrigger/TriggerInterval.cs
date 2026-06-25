namespace HumanReadableTrigger;

/// <summary>
/// A recurring interval expressed as a <paramref name="Count"/> of a
/// <paramref name="Unit"/>, for example 5 <see cref="TimeUnit.Minute"/>s.
/// </summary>
/// <param name="Count">The number of units; always greater than zero.</param>
/// <param name="Unit">The unit of time.</param>
public readonly record struct TriggerInterval(int Count, TimeUnit Unit)
{
    /// <summary>
    /// Converts this interval to a <see cref="TimeSpan"/>.
    /// </summary>
    /// <returns>The equivalent <see cref="TimeSpan"/>.</returns>
    public TimeSpan ToTimeSpan() => Unit switch
    {
        TimeUnit.Second => TimeSpan.FromSeconds(Count),
        TimeUnit.Minute => TimeSpan.FromMinutes(Count),
        TimeUnit.Hour => TimeSpan.FromHours(Count),
        TimeUnit.Day => TimeSpan.FromDays(Count),
        _ => throw new InvalidOperationException($"Unsupported time unit '{Unit}'."),
    };
}
