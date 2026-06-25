namespace HumanReadableTrigger.Tests;

public class RunScheduleTests
{
    // 2026-06-25T12:00:00Z is a Thursday.
    private static readonly DateTimeOffset Reference = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo Plus5 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+5", TimeSpan.FromHours(5), "UTC+5", "UTC+5");

    // ---- Weekly -----------------------------------------------------------

    [Fact]
    public void Weekly_FindsNextMatchingWeekday()
    {
        var schedule = RunSchedule.Parse("9am every Tuesday", Utc);

        Assert.Equal(ScheduleKind.Weekly, schedule.Kind);
        Assert.Equal(new TimeOnly(9, 0), schedule.TimeOfDay);
        Assert.Equal(new[] { DayOfWeek.Tuesday }, schedule.DaysOfWeek);
        // Next Tuesday after Thu 2026-06-25 is 2026-06-30.
        Assert.Equal(new DateTimeOffset(2026, 6, 30, 9, 0, 0, TimeSpan.Zero), schedule.GetNextRun(Reference));
    }

    [Fact]
    public void Weekly_AppliesTimeZoneOffset()
    {
        var schedule = RunSchedule.Parse("9am every Tuesday", Plus5);
        var next = schedule.GetNextRun(Reference)!.Value;

        Assert.Equal(TimeSpan.FromHours(5), next.Offset);
        Assert.Equal(new DateTime(2026, 6, 30, 4, 0, 0), next.UtcDateTime);
    }

    [Fact]
    public void Weekly_TodayCountsWhenTimeIsStillAhead()
    {
        // Reference is 12:00 Thursday; 5pm Thursday is later today.
        var schedule = RunSchedule.Parse("5pm every Monday and Thursday", Utc);

        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Thursday }, schedule.DaysOfWeek);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 17, 0, 0, TimeSpan.Zero), schedule.GetNextRun(Reference));
    }

    [Fact]
    public void Daily_RollsToTomorrowWhenTimeHasPassed()
    {
        // 9am today already passed at 12:00.
        var schedule = RunSchedule.Parse("9am daily", Utc);

        Assert.Equal(ScheduleKind.Weekly, schedule.Kind);
        Assert.Equal(7, schedule.DaysOfWeek!.Length);
        Assert.Equal(new DateTimeOffset(2026, 6, 26, 9, 0, 0, TimeSpan.Zero), schedule.GetNextRun(Reference));
    }

    // ---- Monthly ----------------------------------------------------------

    [Fact]
    public void Monthly_FindsNextDayOfMonth()
    {
        var schedule = RunSchedule.Parse("5pm every 15th", Utc);

        Assert.Equal(ScheduleKind.Monthly, schedule.Kind);
        Assert.Equal(15, schedule.DayOfMonth);
        Assert.Equal(new TimeOnly(17, 0), schedule.TimeOfDay);
        // The 15th already passed in June, so July 15th.
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 17, 0, 0, TimeSpan.Zero), schedule.GetNextRun(Reference));
    }

    [Fact]
    public void Monthly_ClampsDayToShortMonths()
    {
        var schedule = RunSchedule.Parse("9am every 31st", Utc);
        var after = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        // February 2026 has 28 days, so the 31st clamps to the 28th.
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero), schedule.GetNextRun(after));
    }

    // ---- Interval ---------------------------------------------------------

    [Fact]
    public void Interval_Unbounded_StartsImmediatelyThenEveryInterval()
    {
        var schedule = RunSchedule.Parse("every 2 hours", Utc);

        Assert.Equal(ScheduleKind.Interval, schedule.Kind);
        Assert.Equal(TimeSpan.FromHours(2), schedule.Interval);
        Assert.Null(schedule.Window);

        var runs = schedule.GetUpcoming(Reference, 3).ToList();
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 14, 0, 0, TimeSpan.Zero), runs[0]);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 16, 0, 0, TimeSpan.Zero), runs[1]);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 18, 0, 0, TimeSpan.Zero), runs[2]);
    }

    [Fact]
    public void Interval_Bounded_ConfinesToWindowAndWeekdays()
    {
        var schedule = RunSchedule.Parse("every hour between 9am and 1600 Monday Tuesday Thursday", Utc);

        Assert.Equal(TimeSpan.FromHours(1), schedule.Interval);
        Assert.Equal(new TimeOnly(9, 0), schedule.Window!.Value.Start);
        Assert.Equal(new TimeOnly(16, 0), schedule.Window!.Value.End);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday }, schedule.DaysOfWeek);

        var runs = schedule.GetUpcoming(Reference, 6).ToList();
        // Thursday 2026-06-25: hourly 13:00..16:00 (after the 12:00 reference)...
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 13, 0, 0, TimeSpan.Zero), runs[0]);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 16, 0, 0, TimeSpan.Zero), runs[3]);
        // ...then it skips Fri/Sat/Sun and resumes Monday 2026-06-29 at 09:00.
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.Zero), runs[4]);
    }

    [Fact]
    public void Interval_BareUnitMeansOne()
    {
        var schedule = RunSchedule.Parse("every 30 minutes", Utc);

        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Interval);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 12, 30, 0, TimeSpan.Zero), schedule.GetNextRun(Reference));
    }

    // ---- Continuous sleep -------------------------------------------------

    [Theory]
    [InlineData("continuous sleep 12min")]
    [InlineData("sleep 12 minutes between runs")]
    [InlineData("sleep for 12m")]
    public void ContinuousSleep_AddsGapToReference(string expression)
    {
        var schedule = RunSchedule.Parse(expression, Utc);

        Assert.Equal(ScheduleKind.ContinuousSleep, schedule.Kind);
        Assert.Equal(TimeSpan.FromMinutes(12), schedule.SleepBetween);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 12, 12, 0, TimeSpan.Zero), schedule.GetNextRun(Reference));
    }

    // ---- Cron -------------------------------------------------------------

    [Fact]
    public void Cron_StoredRawAndNotEvaluated()
    {
        var schedule = RunSchedule.Parse("cron: 0 9 * * 2", Utc);

        Assert.Equal(ScheduleKind.Cron, schedule.Kind);
        Assert.Equal("0 9 * * 2", schedule.CronExpression);
        Assert.Null(schedule.GetNextRun(Reference));
        Assert.Empty(schedule.GetUpcoming(Reference, 5));
    }

    [Fact]
    public void Cron_RejectsWrongFieldCount()
    {
        Assert.Null(RunSchedule.TryParse("cron 0 9 *", Utc));
    }

    // ---- Errors / relative expressions are rejected -----------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("now")]
    [InlineData("tomorrow at 9am")]
    [InlineData("in 2 hours")]
    [InlineData("2 hours ago")]
    [InlineData("every")]
    [InlineData("9am every blernsday")]
    public void TryParse_ReturnsNull_ForUnsupportedOrInvalidInput(string expression)
    {
        Assert.Null(RunSchedule.TryParse(expression, Utc));
    }

    [Fact]
    public void Parse_Throws_OnNull()
    {
        Assert.Throws<ArgumentNullException>(() => RunSchedule.Parse(null!, Utc));
    }
}
