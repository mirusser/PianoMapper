using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class NoteReadingModeExtensionsTests
{
    [Theory]
    [InlineData(NoteReadingMode.Off, GradedAxes.None)]
    [InlineData(NoteReadingMode.PitchAndOrder, GradedAxes.Pitch)]
    [InlineData(NoteReadingMode.PitchAndHold, GradedAxes.Pitch | GradedAxes.Duration)]
    [InlineData(NoteReadingMode.PitchAndRhythm, GradedAxes.Pitch | GradedAxes.Onset)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm, GradedAxes.Pitch | GradedAxes.Onset | GradedAxes.Duration)]
    [InlineData(NoteReadingMode.RhythmOnly, GradedAxes.Onset)]
    public void GetGradedAxes_EachMode_MapsToItsAxes(NoteReadingMode mode, GradedAxes expected)
    {
        Assert.Equal(expected, mode.GetGradedAxes());
    }

    [Fact]
    public void GetGradedAxes_EveryDefinedMode_IsCovered()
    {
        // A new mode must be added to the mapping (the switch throws for an unknown member) and to the test above.
        foreach (NoteReadingMode mode in Enum.GetValues<NoteReadingMode>())
        {
            _ = mode.GetGradedAxes();
        }
    }

    [Fact]
    public void GetGradedAxes_UndefinedMode_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((NoteReadingMode)999).GetGradedAxes());
    }

    [Fact]
    public void NoteReadingMode_PersistedNamesAndOrdinalsOfExistingMembersAreStable()
    {
        // Mode names are persisted in history as strings and only ever appended (D3).
        Assert.Equal(0, (int)NoteReadingMode.Off);
        Assert.Equal(1, (int)NoteReadingMode.PitchAndOrder);
        Assert.Equal(2, (int)NoteReadingMode.PitchAndHold);
        Assert.Equal(3, (int)NoteReadingMode.PitchHoldAndRhythm);
        Assert.Equal(4, (int)NoteReadingMode.PitchAndRhythm);
        Assert.Equal(5, (int)NoteReadingMode.RhythmOnly);
    }
}
