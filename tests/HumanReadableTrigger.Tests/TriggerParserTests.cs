using HumanReadableTrigger;

namespace HumanReadableTrigger.Tests;

public class TriggerParserTests
{
    [Theory]
    [InlineData("every 5 minutes", 5, TimeUnit.Minute)]
    [InlineData("every 2 hours", 2, TimeUnit.Hour)]
    [InlineData("every 30 seconds", 30, TimeUnit.Second)]
    [InlineData("every 7 days", 7, TimeUnit.Day)]
    public void Parse_WithCountAndUnit_ReturnsInterval(string expression, int count, TimeUnit unit)
    {
        TriggerInterval interval = TriggerParser.Parse(expression);

        Assert.Equal(count, interval.Count);
        Assert.Equal(unit, interval.Unit);
    }

    [Theory]
    [InlineData("every minute", TimeUnit.Minute)]
    [InlineData("every hour", TimeUnit.Hour)]
    [InlineData("every day", TimeUnit.Day)]
    public void Parse_WithoutCount_DefaultsToOne(string expression, TimeUnit unit)
    {
        TriggerInterval interval = TriggerParser.Parse(expression);

        Assert.Equal(1, interval.Count);
        Assert.Equal(unit, interval.Unit);
    }

    [Theory]
    [InlineData("5 minutes")]
    [InlineData("  EVERY   3   Hours  ")]
    [InlineData("every 1 SECOND")]
    public void Parse_IsCaseAndWhitespaceInsensitive(string expression)
    {
        // Should not throw.
        TriggerInterval interval = TriggerParser.Parse(expression);

        Assert.True(interval.Count > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("every")]
    [InlineData("every 5")]
    [InlineData("every 5 fortnights")]
    [InlineData("every 0 minutes")]
    [InlineData("every -3 minutes")]
    [InlineData("every two minutes")]
    [InlineData("5 minutes please")]
    public void TryParse_WithInvalidExpression_ReturnsFalse(string expression)
    {
        bool result = TriggerParser.TryParse(expression, out TriggerInterval interval);

        Assert.False(result);
        Assert.Equal(default, interval);
    }

    [Fact]
    public void Parse_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TriggerParser.Parse(null!));
    }

    [Fact]
    public void Parse_WithInvalidExpression_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => TriggerParser.Parse("not a trigger"));
    }

    [Theory]
    [InlineData(30, TimeUnit.Second, 30)]
    [InlineData(5, TimeUnit.Minute, 5 * 60)]
    [InlineData(2, TimeUnit.Hour, 2 * 60 * 60)]
    [InlineData(1, TimeUnit.Day, 24 * 60 * 60)]
    public void ToTimeSpan_ReturnsExpectedDuration(int count, TimeUnit unit, double expectedSeconds)
    {
        var interval = new TriggerInterval(count, unit);

        Assert.Equal(expectedSeconds, interval.ToTimeSpan().TotalSeconds);
    }
}
