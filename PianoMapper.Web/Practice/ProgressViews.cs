using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Every value the page derives from the learner's <see cref="SightReadingHistory"/>: the Progress panel's lists, the
/// guided path, the weak notes, and the note mastery that steers exercise generation. All of it is computed once, when
/// the views are built, so the page (which re-renders on every key press) only reads it, and builds new views when the
/// history changes.
/// </summary>
internal sealed class ProgressViews
{
    /// <summary>How many of the newest sessions the Progress panel lists.</summary>
    internal const int RecentSessionCount = 10;

    private ProgressViews(SightReadingHistory history)
    {
        RecentSessions = history.Entries.Take(RecentSessionCount).ToArray();
        WeakestPitches = history.ComputeMasteryWeakestFirst();
        InsufficientDataPitchCount = history.ComputeMastery()
            .Count(mastery => mastery.AttemptCount < SightReadingHistory.MinimumMasteryAttempts);
        Insights = SightReadingInsights.Build(history);
        NoteMasteryWeakestFirst = history.ComputeNoteMasteryWeakestFirst();
        WeakNotes = SightReadingInsights.GetWeakNotes(history);
        Ladder = LevelProgression.Evaluate(history);
        RecommendedExerciseDescription = SightReadingLabels.DescribeLevelProgress(
            Ladder.Levels.First(progress => progress.Level == Ladder.Recommended.Level));
    }

    /// <summary>The views of a history with no sessions, for before anything has loaded.</summary>
    internal static ProgressViews Empty { get; } = Build(SightReadingHistory.Empty);

    /// <summary>The newest sessions, newest first, up to <see cref="RecentSessionCount"/>.</summary>
    internal IReadOnlyList<SightReadingSessionSummary> RecentSessions { get; }

    /// <summary>Pitches with enough attempts to rank, weakest accuracy first.</summary>
    internal IReadOnlyList<PitchMastery> WeakestPitches { get; }

    /// <summary>How many pitches have too few attempts to rank yet.</summary>
    internal int InsufficientDataPitchCount { get; }

    internal SightReadingInsightReport Insights { get; }

    /// <summary>Every note (by pitch and staff) with enough attempts to rank, weakest first: what generation adapts to.</summary>
    internal IReadOnlyList<NoteMastery> NoteMasteryWeakestFirst { get; }

    /// <summary>The weak notes a drill can practise, worst first.</summary>
    internal IReadOnlyList<NoteMastery> WeakNotes { get; }

    internal LevelProgressionReport Ladder { get; }

    /// <summary>The reason line for the level the guided path recommends next.</summary>
    internal string RecommendedExerciseDescription { get; }

    internal static ProgressViews Build(SightReadingHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        return new ProgressViews(history);
    }
}
