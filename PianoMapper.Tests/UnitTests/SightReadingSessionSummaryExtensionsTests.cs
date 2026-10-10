using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingSessionSummaryExtensionsTests
{
    private const string V1Fixture = "sight-reading-history-v1.json";
    private const string V2Fixture = "sight-reading-history-v2.json";
    private const string V3Fixture = "sight-reading-history-v3.json";

    [Theory]
    [InlineData(V1Fixture, true)]
    [InlineData(V2Fixture, false)]
    [InlineData(V3Fixture, false)]
    public void IsLegacy_StoredEntries_OnlySchemaVersionOneIs(string fixture, bool expected)
    {
        Assert.All(Entries(fixture), entry => Assert.Equal(expected, entry.IsLegacy()));
    }

    [Fact]
    public void HasReliablePitchCounts_LegacyEntries_OnlyAPitchOnlyRunIs()
    {
        // The v1 fixture holds a hold-and-rhythm run, a hold run and a pitch-only run, newest first.
        Assert.Equal([false, false, true], Entries(V1Fixture).Select(entry => entry.HasReliablePitchCounts()));
    }

    [Theory]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void HasReliablePitchCounts_EntriesWithTheSeparatedPitchOutcome_AreReliableInEveryMode(string fixture)
    {
        Assert.All(Entries(fixture), entry => Assert.True(entry.HasReliablePitchCounts()));
    }

    [Theory]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void CanBeMatchedToLevel_EntriesWithLayoutRhythmAndPitchOutcome_Can(string fixture)
    {
        Assert.All(Entries(fixture), entry => Assert.True(entry.CanBeMatchedToLevel()));
    }

    [Fact]
    public void CanBeMatchedToLevel_LegacyEntries_CannotBecauseTheyLackTheDetails()
    {
        Assert.All(Entries(V1Fixture), entry => Assert.False(entry.CanBeMatchedToLevel()));
    }

    [Fact]
    public void CanBeMatchedToLevel_LegacySchemaEvenWithTheDetails_Cannot()
    {
        Assert.False((Entries(V3Fixture)[0] with { SchemaVersion = SightReadingSessionSummary.LegacySchemaVersion }).CanBeMatchedToLevel());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CanBeMatchedToLevel_AnyMissingDetailOrNoPrompts_Cannot(int missing)
    {
        SightReadingSessionSummary complete = Entries(V3Fixture)[0];
        SightReadingSessionSummary incomplete = missing switch
        {
            0 => complete with { IsGrandStaff = null },
            1 => complete with { RhythmPreset = null },
            2 => complete with { PitchFirstTryCorrectCount = null },
            _ => complete with { PromptCount = 0 },
        };

        Assert.False(incomplete.CanBeMatchedToLevel());
    }

    [Fact]
    public void PitchFirstTryPercent_LegacyEntries_OnlyAPitchOnlyRunHasOne()
    {
        // 6 of 8 prompts were first-try correct in the legacy pitch-only run; the timed runs fused pitch with timing.
        Assert.Equal([null, null, 75.0], Entries(V1Fixture).Select(entry => entry.PitchFirstTryPercent()));
    }

    [Theory]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void PitchFirstTryPercent_EntriesWithTheSeparatedPitchOutcome_UseIt(string fixture)
    {
        // 6 of 8 and 7 of 8 prompts had the right key first.
        Assert.Equal([75.0, 87.5], Entries(fixture).Select(entry => entry.PitchFirstTryPercent()));
    }

    [Fact]
    public void PitchFirstTryPercent_NoPrompts_IsNull()
    {
        Assert.Null((Entries(V3Fixture)[1] with { PromptCount = 0 }).PitchFirstTryPercent());
    }

    [Theory]
    [InlineData(V1Fixture)]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void TimingCleanPercent_PitchOnlyEntries_IsNull(string fixture)
    {
        SightReadingSessionSummary pitchOnly = Entries(fixture).Single(entry => entry.Mode == NoteReadingMode.PitchAndOrder);

        Assert.Null(pitchOnly.TimingCleanPercent());
    }

    [Fact]
    public void TimingCleanPercent_LegacyTimedEntry_IsNullBecauseItNeverSplitTimingOut()
    {
        Assert.All(
            Entries(V1Fixture).Where(entry => entry.Mode != NoteReadingMode.PitchAndOrder),
            entry => Assert.Null(entry.TimingCleanPercent()));
    }

    [Theory]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void TimingCleanPercent_TimedEntry_IsThePromptsWithoutATimingMistake(string fixture)
    {
        // 2 timing mistakes in 8 prompts.
        SightReadingSessionSummary timed = Entries(fixture).Single(entry => entry.Mode == NoteReadingMode.PitchHoldAndRhythm);

        Assert.Equal(75, timed.TimingCleanPercent());
    }

    [Fact]
    public void TimingCleanPercent_TimedEntryWithoutTheMistakeCount_IsNull()
    {
        SightReadingSessionSummary timed = Entries(V3Fixture)[0] with { TimingMistakeCount = null };

        Assert.Null(timed.TimingCleanPercent());
    }

    [Fact]
    public void TimingCleanPercent_RhythmOnly_CountsEveryPromptThatWasNotCleanIncludingUnplayedNotes()
    {
        // Four of ten notes were never played: not timing mistakes in the stored counts, but not clean either.
        SightReadingSessionSummary rhythmOnly = Entries(V3Fixture)[0] with
        {
            Mode = NoteReadingMode.RhythmOnly,
            PromptCount = 10,
            FirstTryCorrectCount = 6,
            TimingMistakeCount = 0,
        };

        Assert.Equal(60, rhythmOnly.TimingCleanPercent());
    }

    [Fact]
    public void TimingCleanPercent_NoPrompts_IsNull()
    {
        Assert.Null((Entries(V3Fixture)[0] with { PromptCount = 0 }).TimingCleanPercent());
    }

    [Theory]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void GetTimingBreakdown_TimedEntry_SplitsPitchFromTiming(string fixture)
    {
        SightReadingSessionSummary timed = Entries(fixture).Single(entry => entry.Mode == NoteReadingMode.PitchHoldAndRhythm);

        SessionTimingBreakdown breakdown = Assert.IsType<SessionTimingBreakdown>(timed.GetTimingBreakdown());

        Assert.Equal(6, breakdown.PitchFirstTryCorrectCount);
        Assert.Equal(2, breakdown.TimingMistakeCount);
    }

    [Theory]
    [InlineData(V1Fixture)]
    [InlineData(V2Fixture)]
    [InlineData(V3Fixture)]
    public void GetTimingBreakdown_PitchOnlyEntry_IsNull(string fixture)
    {
        Assert.Null(Entries(fixture).Single(entry => entry.Mode == NoteReadingMode.PitchAndOrder).GetTimingBreakdown());
    }

    [Fact]
    public void GetTimingBreakdown_LegacyTimedEntries_AreNullBecauseSchemaOneFusedThem()
    {
        Assert.All(
            Entries(V1Fixture).Where(entry => entry.Mode != NoteReadingMode.PitchAndOrder),
            entry => Assert.Null(entry.GetTimingBreakdown()));
    }

    [Fact]
    public void GetTimingBreakdown_TimedEntryMissingEitherCount_IsNull()
    {
        SightReadingSessionSummary timed = Entries(V3Fixture)[0];

        Assert.Null((timed with { PitchFirstTryCorrectCount = null }).GetTimingBreakdown());
        Assert.Null((timed with { TimingMistakeCount = null }).GetTimingBreakdown());
    }

    private static IReadOnlyList<SightReadingSessionSummary> Entries(string fixture) =>
        SightReadingHistory.FromJson(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture))).Entries;
}
