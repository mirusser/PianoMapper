using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ProgressViewsTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch D4 = new(NoteLetter.D, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_EmptyHistory_HasNothingToShowAndRecommendsTheFirstLevel()
    {
        ProgressViews views = ProgressViews.Build(SightReadingHistory.Empty);

        Assert.Empty(views.RecentSessions);
        Assert.Empty(views.WeakestPitches);
        Assert.Equal(0, views.InsufficientDataPitchCount);
        Assert.Empty(views.NoteMasteryWeakestFirst);
        Assert.Empty(views.WeakNotes);
        Assert.False(views.Insights.HasEnoughData);
        Assert.Equal(1, views.Ladder.Recommended.Level.Number);
    }

    [Fact]
    public void Empty_IsTheViewsOfAnEmptyHistory()
    {
        Assert.Empty(ProgressViews.Empty.RecentSessions);
        Assert.Equal(1, ProgressViews.Empty.Ladder.Recommended.Level.Number);
        Assert.Contains(ProgressViews.Empty.Ladder.Recommended.Level.Name, ProgressViews.Empty.RecommendedExerciseDescription);
    }

    [Fact]
    public void Build_MoreSessionsThanTheRecentCount_KeepsTheNewestInOrder()
    {
        SightReadingSessionSummary[] sessions = Enumerable.Range(0, ProgressViews.RecentSessionCount + 5)
            .Select(index => Session(index))
            .ToArray();
        SightReadingHistory history = History(sessions);

        ProgressViews views = ProgressViews.Build(history);

        Assert.Equal(ProgressViews.RecentSessionCount, views.RecentSessions.Count);
        Assert.Equal(
            sessions.OrderByDescending(session => session.CompletedAt).Take(ProgressViews.RecentSessionCount),
            views.RecentSessions);
    }

    [Fact]
    public void Build_PitchesWithAndWithoutEnoughAttempts_RanksTheFormerAndCountsTheLatter()
    {
        SightReadingHistory history = History(Session(
            0,
            pitchAttempts:
            [
                new PitchAttemptSummary(C4, 2, SightReadingHistory.MinimumMasteryAttempts),
                new PitchAttemptSummary(D4, 1, SightReadingHistory.MinimumMasteryAttempts - 1),
                new PitchAttemptSummary(E4, 0, 1),
            ]));

        ProgressViews views = ProgressViews.Build(history);

        Assert.Equal([C4], views.WeakestPitches.Select(mastery => mastery.Pitch));
        Assert.Equal(2, views.InsufficientDataPitchCount);
    }

    [Fact]
    public void Build_NoteMastery_ListsEnoughDataNotesWorstFirstAndWeakNotesAreTheOnesPastTheThreshold()
    {
        SightReadingHistory history = History(Session(
            0,
            noteAttempts:
            [
                new NoteAttemptSummary(C4, Staff.Bass, 10, 2, 0, null),
                new NoteAttemptSummary(D4, Staff.Treble, 10, 7, 0, null),
                new NoteAttemptSummary(E4, Staff.Treble, 10, 10, 0, null),
            ]));

        ProgressViews views = ProgressViews.Build(history);

        Assert.Equal([C4, D4, E4], views.NoteMasteryWeakestFirst.Select(mastery => mastery.Pitch));
        Assert.Equal([C4, D4], views.WeakNotes.Select(mastery => mastery.Pitch));
    }

    [Fact]
    public void Build_Insights_CoverTheSessionsOfTheHistory()
    {
        SightReadingHistory history = History(Session(0), Session(1), Session(2));

        Assert.Equal(3, ProgressViews.Build(history).Insights.Trend.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public void Build_RecommendedExerciseDescription_NamesTheRecommendedLevelWhicheverPlaceItHasInTheCatalog(int passedLevelCount)
    {
        // Passing the first levels moves the recommendation along; passing all of them leaves it on the last level.
        SightReadingHistory history = History(PassingSessions(ExerciseLevelCatalog.Levels.Take(passedLevelCount)));

        ProgressViews views = ProgressViews.Build(history);

        LevelRecommendation expected = LevelProgression.Evaluate(history).Recommended;
        Assert.Equal(expected, views.Ladder.Recommended);
        Assert.Contains(expected.Level.Name, views.RecommendedExerciseDescription);
    }

    [Fact]
    public void Views_ReadMoreThanOnce_ReturnTheSameValuesInsteadOfComputingThemAgain()
    {
        ProgressViews views = ProgressViews.Build(History(Session(0), Session(1)));

        Assert.Same(views.RecentSessions, views.RecentSessions);
        Assert.Same(views.WeakestPitches, views.WeakestPitches);
        Assert.Same(views.NoteMasteryWeakestFirst, views.NoteMasteryWeakestFirst);
        Assert.Same(views.WeakNotes, views.WeakNotes);
        Assert.Same(views.Insights, views.Insights);
        Assert.Same(views.Ladder, views.Ladder);
    }

    private static SightReadingHistory History(params SightReadingSessionSummary[] sessions)
    {
        SightReadingHistory history = SightReadingHistory.Empty;
        foreach (SightReadingSessionSummary session in sessions)
        {
            history = history.WithCompletedSession(session);
        }

        return history;
    }

    /// <summary>Three clean, on-time sessions for each level, which is what passes it.</summary>
    private static SightReadingSessionSummary[] PassingSessions(IEnumerable<ExerciseLevel> levels) =>
        [.. levels.SelectMany((level, levelIndex) => Enumerable.Range(0, 3).Select(repeat =>
            Session(levelIndex * 3 + repeat) with
            {
                PresetId = level.PresetId.ToString(),
                Staff = level.Staff,
                Mode = level.Mode,
                RhythmPreset = level.RhythmPreset.ToString(),
                IsGrandStaff = level.IsGrandStaff,
                TempoBeatsPerMinute = level.StartTempoPulsesPerMinute,
            }))];

    private static SightReadingSessionSummary Session(
        int index,
        IReadOnlyList<PitchAttemptSummary>? pitchAttempts = null,
        IReadOnlyList<NoteAttemptSummary>? noteAttempts = null) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            Guid.NewGuid(),
            Start.AddHours(index),
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            8,
            TimeSpan.FromSeconds(30),
            8,
            0,
            pitchAttempts ?? [])
        {
            RhythmPreset = "Fixed",
            IsGrandStaff = false,
            PitchFirstTryCorrectCount = 8,
            TimingMistakeCount = 0,
            NoteAttempts = noteAttempts ?? [],
            Confusions = [],
        };
}
