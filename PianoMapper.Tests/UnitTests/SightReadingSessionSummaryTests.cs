using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingSessionSummaryTests
{
    [Fact]
    public void Create_MixOfFirstTryAndRetriedPrompts_AggregatesCountsCorrectly()
    {
        var completedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        NoteReadingPromptResult[] results =
        [
            CreatePromptResult(0, NoteLetter.C, isFirstTryCorrect: true, wrongAttemptCount: 0),
            CreatePromptResult(1, NoteLetter.D, isFirstTryCorrect: false, wrongAttemptCount: 2),
        ];

        SightReadingSessionSummary summary = SightReadingSessionSummary.Create(
            completedAt,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.FromSeconds(30),
            results);

        Assert.Equal(SightReadingSessionSummary.CurrentSchemaVersion, summary.SchemaVersion);
        Assert.Equal(completedAt, summary.CompletedAt);
        Assert.Equal("FiveNote", summary.PresetId);
        Assert.Equal(Staff.Treble, summary.Staff);
        Assert.Equal(NoteReadingMode.PitchAndOrder, summary.Mode);
        Assert.Equal(2, summary.PromptCount);
        Assert.Equal(TimeSpan.FromSeconds(30), summary.ElapsedTime);
        Assert.Equal(1, summary.FirstTryCorrectCount);
        Assert.Equal(2, summary.WrongAttemptCount);
        Assert.Equal(2, summary.PitchAttempts.Count);

        PitchAttemptSummary cAttempt = Assert.Single(
            summary.PitchAttempts,
            attempt => attempt.Pitch == new Pitch(NoteLetter.C, 0, 4));
        Assert.Equal(1, cAttempt.CorrectFirstTryCount);
        Assert.Equal(1, cAttempt.AttemptCount);

        PitchAttemptSummary dAttempt = Assert.Single(
            summary.PitchAttempts,
            attempt => attempt.Pitch == new Pitch(NoteLetter.D, 0, 4));
        Assert.Equal(0, dAttempt.CorrectFirstTryCount);
        Assert.Equal(1, dAttempt.AttemptCount);
    }

    [Fact]
    public void Create_NullOrWhitespacePresetId_Throws()
    {
        Assert.Throws<ArgumentException>(() => SightReadingSessionSummary.Create(
            DateTimeOffset.UtcNow,
            " ",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.Zero,
            []));
    }

    private static NoteReadingPromptResult CreatePromptResult(
        int promptIndex,
        NoteLetter letter,
        bool isFirstTryCorrect,
        int wrongAttemptCount)
    {
        var pitch = new Pitch(letter, 0, 4);
        var note = new ScoreNote(pitch, new NoteValue(4), promptIndex, promptIndex, Staff.Treble);
        return new NoteReadingPromptResult(
            promptIndex,
            promptIndex,
            [note],
            [pitch],
            [],
            wrongAttemptCount,
            isFirstTryCorrect,
            IsComplete: true,
            CompletedAt: TimeSpan.FromSeconds(promptIndex));
    }
}
