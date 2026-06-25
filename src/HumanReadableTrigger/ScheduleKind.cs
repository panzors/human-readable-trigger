namespace HumanReadableTrigger;

/// <summary>
/// The category of a parsed <see cref="RunSchedule"/>.
/// </summary>
public enum ScheduleKind
{
    /// <summary>Runs at a time of day on one or more days of the week (e.g. "9am every Tuesday").</summary>
    Weekly,

    /// <summary>Runs at a time of day on a specific day of the month (e.g. "5pm every 15th").</summary>
    Monthly,

    /// <summary>Runs on a fixed clock interval (e.g. "every 2 hours"), optionally confined to a window and days.</summary>
    Interval,

    /// <summary>Runs continuously, sleeping a fixed gap between runs (e.g. "continuous sleep 12min").</summary>
    ContinuousSleep,

    /// <summary>A raw cron expression, stored and validated but not evaluated by this library.</summary>
    Cron,
}
