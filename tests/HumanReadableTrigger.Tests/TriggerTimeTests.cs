namespace HumanReadableTrigger.Tests;

public class TriggerTimeTests
{
    // A fixed, environment-independent reference instant: 2026-06-25T12:00:00Z.
    // (2026-06-25 is a Thursday.)
    private static readonly DateTimeOffset Reference =
        new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);

    // A fixed-offset (UTC+5) zone so tests do not depend on the host's installed zones.
    private static readonly TimeZoneInfo TzPlus5 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+5", TimeSpan.FromHours(5), "UTC+5", "UTC+5");

    [Fact]
    public void Now_PreservesInstant_AndAppliesOverrideOffset()
    {
        var trigger = new TriggerTime("now", TzPlus5, Reference);

        Assert.Equal(Reference.UtcDateTime, trigger.Utc.UtcDateTime);
        Assert.Equal(TimeSpan.FromHours(5), trigger.Value.Offset);
        Assert.Equal(new DateTime(2026, 6, 25, 17, 0, 0), trigger.Value.DateTime);
    }

    [Fact]
    public void DefaultsToLocalTimeZone_WhenNoOverride()
    {
        var trigger = new TriggerTime("now");

        Assert.Equal(TimeZoneInfo.Local, trigger.TimeZone);
    }

    [Theory]
    [InlineData("in 30 minutes", 30)]
    [InlineData("in 1 hour", 60)]
    [InlineData("in 2 hours", 120)]
    [InlineData("in 1 day", 1440)]
    public void In_AddsDurationToReference(string expression, int expectedMinutes)
    {
        var trigger = new TriggerTime(expression, TzPlus5, Reference);

        Assert.Equal(Reference.AddMinutes(expectedMinutes).UtcDateTime, trigger.Utc.UtcDateTime);
    }

    [Fact]
    public void In_SupportsCompoundDurations()
    {
        var trigger = new TriggerTime("in 1 hour 30 minutes", TzPlus5, Reference);

        Assert.Equal(Reference.AddMinutes(90).UtcDateTime, trigger.Utc.UtcDateTime);
    }

    [Theory]
    [InlineData("2 hours ago", -120)]
    [InlineData("15 minutes ago", -15)]
    [InlineData("1 week ago", -10080)]
    public void Ago_SubtractsDurationFromReference(string expression, int deltaMinutes)
    {
        var trigger = new TriggerTime(expression, TzPlus5, Reference);

        Assert.Equal(Reference.AddMinutes(deltaMinutes).UtcDateTime, trigger.Utc.UtcDateTime);
    }

    [Fact]
    public void TodayAt_UsesReferenceDateInTargetZone()
    {
        // Reference is 17:00 local in UTC+5 on 2026-06-25.
        var trigger = new TriggerTime("today at 9am", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 6, 25, 9, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
        Assert.Equal(new DateTime(2026, 6, 25, 4, 0, 0), trigger.Utc.UtcDateTime);
    }

    [Fact]
    public void TomorrowAt_AdvancesOneDay()
    {
        var trigger = new TriggerTime("tomorrow at 18:30", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 6, 26, 18, 30, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void YesterdayAt_GoesBackOneDay()
    {
        var trigger = new TriggerTime("yesterday at noon", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void BareTime_ResolvesToToday()
    {
        var trigger = new TriggerTime("9:15pm", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 6, 25, 21, 15, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void At_KeywordIsOptionalSeparator()
    {
        var trigger = new TriggerTime("at midnight", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void Weekday_ResolvesToNextUpcomingOccurrence()
    {
        // 2026-06-25 is Thursday; the upcoming Friday is the next day.
        var trigger = new TriggerTime("friday", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void NextWeekday_SkipsToFollowingWeek_WhenTodayMatches()
    {
        // Today is Thursday; "next thursday" is strictly after today => +7 days.
        var trigger = new TriggerTime("next thursday at 9am", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void Absolute_WithExplicitZone_IsHonoredAndConverted()
    {
        var trigger = new TriggerTime("2026-06-25T12:00:00Z", TzPlus5, Reference);

        Assert.Equal(Reference.UtcDateTime, trigger.Utc.UtcDateTime);
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 17, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
    }

    [Fact]
    public void Absolute_WithoutZone_IsInterpretedInOverride()
    {
        var trigger = new TriggerTime("2026-12-25 08:00", TzPlus5, Reference);

        Assert.Equal(new DateTimeOffset(2026, 12, 25, 8, 0, 0, TimeSpan.FromHours(5)), trigger.Value);
        Assert.Equal(new DateTime(2026, 12, 25, 3, 0, 0), trigger.Utc.UtcDateTime);
    }

    [Fact]
    public void TimeZoneOverride_ChangesTheResultingInstant()
    {
        var inPlus5 = new TriggerTime("today at 9am", TzPlus5, Reference);

        var tzMinus3 = TimeZoneInfo.CreateCustomTimeZone("UTC-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");
        var inMinus3 = new TriggerTime("today at 9am", tzMinus3, Reference);

        // Same wall-clock time, different zone => different absolute instants.
        Assert.NotEqual(inPlus5.Utc.UtcDateTime, inMinus3.Utc.UtcDateTime);
        Assert.Equal(new DateTime(2026, 6, 25, 4, 0, 0), inPlus5.Utc.UtcDateTime);
        Assert.Equal(new DateTime(2026, 6, 25, 12, 0, 0), inMinus3.Utc.UtcDateTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a real expression")]
    [InlineData("in five minutes")]
    [InlineData("in 5 fortnights")]
    [InlineData("25:00")]
    public void TryParse_ReturnsNull_ForUnparseableInput(string expression)
    {
        Assert.Null(TriggerTime.TryParse(expression, TzPlus5, Reference));
    }

    [Fact]
    public void Constructor_Throws_OnNullExpression()
    {
        Assert.Throws<ArgumentNullException>(() => new TriggerTime(null!, TzPlus5, Reference));
    }

    [Fact]
    public void Constructor_Throws_FormatException_OnGarbage()
    {
        Assert.Throws<FormatException>(() => new TriggerTime("wibble", TzPlus5, Reference));
    }

    [Fact]
    public void ToString_ReturnsRoundTripFormat()
    {
        var trigger = new TriggerTime("today at 9am", TzPlus5, Reference);

        Assert.Equal(trigger.Value.ToString("O"), trigger.ToString());
    }
}
