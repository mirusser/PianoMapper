using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingExerciseLengthTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(60);

    [Fact]
    public void PromptCountChoices_OfferLengthsBeyondSixteenNotes()
    {
        Assert.Contains(8, SightReadingLabels.PromptCountChoices);
        Assert.Contains(16, SightReadingLabels.PromptCountChoices);
        Assert.Contains(SightReadingLabels.PromptCountChoices, choice => choice > 16);
        Assert.Equal(SightReadingLabels.PromptCountChoices.Order(), SightReadingLabels.PromptCountChoices);
        Assert.Equal(SightReadingLabels.PromptCountChoices.Distinct().Count(), SightReadingLabels.PromptCountChoices.Count);
    }

    [Theory]
    [InlineData(8, "8 notes")]
    [InlineData(16, "16 notes")]
    [InlineData(32, "32 notes")]
    [InlineData(64, "64 notes")]
    public void PromptCountOption_NamesTheNoteCount(int promptCount, string expected)
    {
        Assert.Equal(expected, SightReadingLabels.PromptCountOption(promptCount));
    }

    [Fact]
    public void PromptCountChoices_EveryChoiceComposesAFullExerciseForEveryRangeStaffLayoutAndRhythm()
    {
        foreach (int promptCount in SightReadingLabels.PromptCountChoices)
        {
            foreach (SightReadingPresetId preset in Enum.GetValues<SightReadingPresetId>())
            {
                foreach (SightReadingRhythmPreset rhythm in Enum.GetValues<SightReadingRhythmPreset>())
                {
                    foreach (bool isGrandStaff in new[] { false, true })
                    {
                        if (isGrandStaff && preset == SightReadingPresetId.Chords)
                        {
                            continue;
                        }

                        var options = new SightReadingExerciseOptions(
                            Staff.Treble,
                            preset,
                            promptCount,
                            NoteReadingMode.PitchAndOrder,
                            isGrandStaff,
                            rhythm);

                        Score score = SightReadingExerciseComposer.Compose(options, new Random(promptCount));
                        var session = new NoteReadingSession();
                        session.Reset(score, NoteReadingMode.PitchAndOrder, Tolerance);

                        Assert.True(
                            session.PromptCount >= promptCount,
                            $"{promptCount} notes, {preset}, {rhythm}, grand staff {isGrandStaff} gave {session.PromptCount} prompts.");
                    }
                }
            }
        }
    }

    [Fact]
    public void PromptCountChoices_EveryChoiceComposesWithHandsTogether()
    {
        foreach (int promptCount in SightReadingLabels.PromptCountChoices)
        {
            var options = new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                promptCount,
                NoteReadingMode.PitchAndOrder,
                IsGrandStaff: true,
                IsHandsTogether: true);

            Score score = SightReadingExerciseComposer.Compose(options, new Random(promptCount));
            var session = new NoteReadingSession();
            session.Reset(score, NoteReadingMode.PitchAndOrder, Tolerance);

            Assert.Equal(promptCount, session.PromptCount);
        }
    }
}
