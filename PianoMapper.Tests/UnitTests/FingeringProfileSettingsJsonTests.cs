using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class FingeringProfileSettingsJsonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void Parse_MissingOrUnreadableStorage_ReturnsTheDefault(string? json)
    {
        Assert.Equal(FingeringProfileSettings.Default, FingeringProfileSettingsJson.Parse(json));
    }

    [Fact]
    public void Parse_StoredReaches_AreRestored()
    {
        FingeringProfileSettings settings = FingeringProfileSettingsJson.Parse(
            """{"rightComfortable":5,"rightMaximum":7.5,"leftComfortable":4,"leftMaximum":6}""");

        Assert.Equal(new FingeringProfileSettings(5, 7.5, 4, 6), settings);
    }

    [Fact]
    public void Parse_OneHandMissing_KeepsTheStoredHandAndDefaultsTheOther()
    {
        FingeringProfileSettings settings = FingeringProfileSettingsJson.Parse("""{"rightComfortable":5,"rightMaximum":7}""");

        Assert.Equal(5, settings.RightComfortable);
        Assert.Equal(7, settings.RightMaximum);
        Assert.Equal(FingeringProfileSettings.Default.LeftComfortable, settings.LeftComfortable);
        Assert.Equal(FingeringProfileSettings.Default.LeftMaximum, settings.LeftMaximum);
    }

    [Theory]
    [InlineData("""{"rightComfortable":9,"rightMaximum":7,"leftComfortable":4,"leftMaximum":6}""")]
    [InlineData("""{"rightComfortable":5,"rightMaximum":13,"leftComfortable":4,"leftMaximum":6}""")]
    [InlineData("""{"rightComfortable":"wide","rightMaximum":7,"leftComfortable":4,"leftMaximum":6}""")]
    public void Parse_InvalidHand_DefaultsThatHandWithoutLosingTheOther(string json)
    {
        FingeringProfileSettings settings = FingeringProfileSettingsJson.Parse(json);

        Assert.Equal(FingeringProfileSettings.Default.RightComfortable, settings.RightComfortable);
        Assert.Equal(FingeringProfileSettings.Default.RightMaximum, settings.RightMaximum);
        Assert.Equal(4, settings.LeftComfortable);
        Assert.Equal(6, settings.LeftMaximum);
        Assert.True(settings.IsValid);
    }
}
