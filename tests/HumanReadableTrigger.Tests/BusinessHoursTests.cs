namespace HumanReadableTrigger.Tests;

public class BusinessHoursTests
{
    [Fact]
    public void Constructor_StoresStartAndEnd()
    {
        var hours = new BusinessHours(new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.Equal(new TimeOnly(9, 0), hours.Start);
        Assert.Equal(new TimeOnly(17, 0), hours.End);
    }

    [Theory]
    [InlineData(9, 9)]
    [InlineData(17, 9)]
    public void Constructor_Rejects_EndNotAfterStart(int endHour, int startHour)
    {
        Assert.Throws<ArgumentException>(
            () => new BusinessHours(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0)));
    }

    [Fact]
    public void InvalidWindow_MakesTryParseReturnNull()
    {
        // 5pm is not after 9am-as-end => invalid window => null.
        Assert.Null(RunSchedule.TryParse("every hour between 5pm and 9am", TimeZoneInfo.Utc));
    }
}
