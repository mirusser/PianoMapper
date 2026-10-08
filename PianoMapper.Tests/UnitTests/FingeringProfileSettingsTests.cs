using PianoMapper.Music;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class FingeringProfileSettingsTests
{
    [Fact]
    public void Default_ToProfile_UsesTheCoreDefaultThumbToLittleFingerReach()
    {
        ScoreFingeringProfile profile = FingeringProfileSettings.Default.ToProfile();

        FingeringReach expected = ScoreFingeringProfile.DefaultThumbToLittleFingerReach;
        Assert.Equal(expected.ComfortableWhiteKeySpan, profile.RightHand.GetReach(1, 5).ComfortableWhiteKeySpan);
        Assert.Equal(expected.MaximumWhiteKeySpan, profile.RightHand.GetReach(1, 5).MaximumWhiteKeySpan);
        Assert.Equal(expected.MaximumWhiteKeySpan, profile.LeftHand.GetReach(1, 5).MaximumWhiteKeySpan);
    }

    [Fact]
    public void ToProfile_DifferentHands_AreMappedIndependently()
    {
        var settings = new FingeringProfileSettings(5, 7, 4, 6);

        ScoreFingeringProfile profile = settings.ToProfile();

        Assert.Equal(7, profile.RightHand.GetReach(1, 5).MaximumWhiteKeySpan);
        Assert.Equal(4, profile.LeftHand.GetReach(1, 5).ComfortableWhiteKeySpan);
        Assert.Equal(6, profile.LeftHand.GetReach(1, 5).MaximumWhiteKeySpan);
    }

    [Theory]
    [InlineData(Staff.Treble, false, "5.5", 5.5, 9, 4, 8)]
    [InlineData(Staff.Treble, true, "7", 6, 7, 4, 8)]
    [InlineData(Staff.Bass, false, "3", 6, 9, 3, 8)]
    [InlineData(Staff.Bass, true, "10.5", 6, 9, 4, 10.5)]
    public void TryEditReach_ValidEdit_ChangesOnlyThatFieldAndReportsNoRejection(
        Staff hand,
        bool isMaximum,
        string text,
        double rightComfortable,
        double rightMaximum,
        double leftComfortable,
        double leftMaximum)
    {
        var original = new FingeringProfileSettings(6, 9, 4, 8);

        bool accepted = original.TryEditReach(hand, isMaximum, text, out FingeringProfileSettings edited, out string? rejection);

        Assert.True(accepted);
        Assert.Null(rejection);
        Assert.Equal(new FingeringProfileSettings(rightComfortable, rightMaximum, leftComfortable, leftMaximum), edited);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wide")]
    [InlineData("99")]
    [InlineData("-3")]
    public void TryEditReach_TextThatIsNotASupportedReach_IsRejectedAndKeepsTheLastAcceptedValues(string text)
    {
        var original = new FingeringProfileSettings(6, 9, 4, 8);

        bool accepted = original.TryEditReach(Staff.Treble, isMaximum: true, text, out FingeringProfileSettings edited, out string? rejection);

        Assert.False(accepted);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(original, edited);
    }

    [Theory]
    [InlineData(Staff.Treble, false, "11")]
    [InlineData(Staff.Bass, true, "3")]
    public void TryEditReach_ComfortableAndMaximumOutOfOrder_IsRejectedAndKeepsTheLastAcceptedValues(
        Staff hand,
        bool isMaximum,
        string text)
    {
        var original = new FingeringProfileSettings(6, 9, 4, 8);

        bool accepted = original.TryEditReach(hand, isMaximum, text, out FingeringProfileSettings edited, out string? rejection);

        Assert.False(accepted);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(original, edited);
    }
}
