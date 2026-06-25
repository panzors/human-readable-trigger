namespace HumanReadableTrigger.Tests;

public class ScheduleSerializationTests
{
    private static readonly DateTimeOffset Reference = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);

    // Use a real system zone so TimeZoneId round-trips via FindSystemTimeZoneById.
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void Weekly_RoundTripsThroughJson()
    {
        var original = RunSchedule.Parse("9am every Tuesday and Thursday", Utc);

        var restored = RunSchedule.FromJson(original.ToJson());

        Assert.Equal(ScheduleKind.Weekly, restored.Kind);
        Assert.Equal("UTC", restored.TimeZoneId);
        Assert.Equal(new TimeOnly(9, 0), restored.TimeOfDay);
        Assert.Equal(new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }, restored.DaysOfWeek);
        Assert.Equal(original.GetNextRun(Reference), restored.GetNextRun(Reference));
    }

    [Fact]
    public void Interval_WithWindow_RoundTripsThroughJson()
    {
        var original = RunSchedule.Parse("every hour between 9am and 1600 Monday", Utc);

        var restored = RunSchedule.FromJson(original.ToJson());

        Assert.Equal(ScheduleKind.Interval, restored.Kind);
        Assert.Equal(TimeSpan.FromHours(1), restored.Interval);
        Assert.Equal(new TimeOnly(9, 0), restored.Window!.Value.Start);
        Assert.Equal(new TimeOnly(16, 0), restored.Window!.Value.End);
        Assert.Equal(new[] { DayOfWeek.Monday }, restored.DaysOfWeek);
        Assert.Equal(original.GetNextRun(Reference), restored.GetNextRun(Reference));
    }

    [Fact]
    public void Cron_RoundTripsThroughJson()
    {
        var original = RunSchedule.Parse("cron 0 9 * * 1-5", Utc);

        var restored = RunSchedule.FromJson(original.ToJson());

        Assert.Equal(ScheduleKind.Cron, restored.Kind);
        Assert.Equal("0 9 * * 1-5", restored.CronExpression);
    }

    [Fact]
    public void Json_OmitsNullMembers()
    {
        var json = RunSchedule.Parse("every 2 hours", Utc).ToJson();

        Assert.Contains("\"Interval\"", json);
        Assert.DoesNotContain("CronExpression", json);
        Assert.DoesNotContain("DayOfMonth", json);
        Assert.DoesNotContain("Window", json);
    }
}
