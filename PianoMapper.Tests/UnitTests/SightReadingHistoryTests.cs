using System.Text.Json;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingHistoryTests
{
    [Fact]
    public void FromJson_NullOrWhitespaceJson_ReturnsEmptyHistory()
    {
        SightReadingHistory history = SightReadingHistory.FromJson(null);

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void FromJson_MalformedJson_ReturnsEmptyHistory()
    {
        SightReadingHistory history = SightReadingHistory.FromJson("{ not valid json");

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void FromJson_JsonThatIsNotAnArray_ReturnsEmptyHistory()
    {
        SightReadingHistory history = SightReadingHistory.FromJson("""{"foo":"bar"}""");

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void FromJson_ValidEntries_OrdersNewestFirst()
    {
        SightReadingHistory history = SightReadingHistory.Empty
            .WithCompletedSession(CreateSummary(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))
            .WithCompletedSession(CreateSummary(new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)))
            .WithCompletedSession(CreateSummary(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)));

        string json = history.ToJson();
        SightReadingHistory reloaded = SightReadingHistory.FromJson(json);

        Assert.Equal(
            [new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)],
            reloaded.Entries.Select(entry => DateOnly.FromDateTime(entry.CompletedAt.UtcDateTime)));
    }

    [Fact]
    public void WithCompletedSession_MoreThanMaxEntries_RetainsOnlyNewest100()
    {
        SightReadingHistory history = SightReadingHistory.Empty;
        for (int index = 0; index < 105; index++)
        {
            history = history.WithCompletedSession(
                CreateSummary(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index)));
        }

        Assert.Equal(100, history.Entries.Count);
        Assert.Equal(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(104),
            history.Entries[0].CompletedAt);
    }

    [Fact]
    public void FromJson_OneMalformedEntryAmongValidOnes_SkipsOnlyThatEntry()
    {
        SightReadingSessionSummary validEntry = CreateSummary(DateTimeOffset.UtcNow);
        string validJson = SightReadingHistory.Empty.WithCompletedSession(validEntry).ToJson();
        string validElement = JsonDocument.Parse(validJson).RootElement[0].GetRawText();
        string combinedJson = $"[{validElement}, {{\"not\":\"a summary\"}}, \"just a string\"]";

        SightReadingHistory history = SightReadingHistory.FromJson(combinedJson);

        SightReadingSessionSummary onlyEntry = Assert.Single(history.Entries);
        Assert.Equal(validEntry.PresetId, onlyEntry.PresetId);
    }

    [Fact]
    public void FromJson_UnknownSchemaVersion_SkipsThatEntry()
    {
        SightReadingSessionSummary futureEntry = CreateSummary(DateTimeOffset.UtcNow) with { SchemaVersion = 999 };
        string json = SightReadingHistory.Empty.WithCompletedSession(futureEntry).ToJson();

        SightReadingHistory history = SightReadingHistory.FromJson(json);

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void ToJson_RoundTripsRepresentativeSummaryThroughSystemTextJson()
    {
        SightReadingSessionSummary original = SightReadingSessionSummary.Create(
            new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            "OneOctave",
            Staff.Bass,
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromSeconds(42.5),
            [
                new NoteReadingPromptResult(
                    0,
                    0,
                    [new ScoreNote(new Pitch(NoteLetter.C, 1, 3), new NoteValue(4), 0, 0, Staff.Bass)],
                    [new Pitch(NoteLetter.C, 1, 3)],
                    [new Pitch(NoteLetter.D, 0, 3)],
                    1,
                    IsFirstTryCorrect: false,
                    IsComplete: true,
                    CompletedAt: TimeSpan.FromSeconds(1)),
            ]);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(original);

        string json = history.ToJson();
        SightReadingHistory reloaded = SightReadingHistory.FromJson(json);

        SightReadingSessionSummary roundTripped = Assert.Single(reloaded.Entries);
        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(original.CompletedAt, roundTripped.CompletedAt);
        Assert.Equal(original.PresetId, roundTripped.PresetId);
        Assert.Equal(original.Staff, roundTripped.Staff);
        Assert.Equal(original.Mode, roundTripped.Mode);
        Assert.Equal(original.PromptCount, roundTripped.PromptCount);
        Assert.Equal(original.ElapsedTime, roundTripped.ElapsedTime);
        Assert.Equal(original.FirstTryCorrectCount, roundTripped.FirstTryCorrectCount);
        Assert.Equal(original.WrongAttemptCount, roundTripped.WrongAttemptCount);
        Assert.Equal(original.PitchAttempts, roundTripped.PitchAttempts);
    }

    [Fact]
    public void ComputeMastery_AggregatesAcrossEntries_ComputesAccuracyWithoutDividingByZero()
    {
        var cSharp4 = new Pitch(NoteLetter.C, 1, 4);
        SightReadingHistory history = SightReadingHistory.Empty
            .WithCompletedSession(CreateSummary(DateTimeOffset.UtcNow, [new PitchAttemptSummary(cSharp4, 1, 2)]))
            .WithCompletedSession(CreateSummary(
                DateTimeOffset.UtcNow.AddMinutes(1),
                [new PitchAttemptSummary(cSharp4, 0, 1), new PitchAttemptSummary(new Pitch(NoteLetter.D, 0, 4), 0, 0)]));

        IReadOnlyList<PitchMastery> mastery = history.ComputeMastery();

        PitchMastery cSharpMastery = Assert.Single(mastery, entry => entry.Pitch == cSharp4);
        Assert.Equal(1, cSharpMastery.CorrectFirstTryCount);
        Assert.Equal(3, cSharpMastery.AttemptCount);
        Assert.Equal(100.0 / 3.0, cSharpMastery.AccuracyPercent, precision: 6);

        PitchMastery dMastery = Assert.Single(mastery, entry => entry.Pitch == new Pitch(NoteLetter.D, 0, 4));
        Assert.Equal(0, dMastery.AttemptCount);
        Assert.Equal(0, dMastery.AccuracyPercent);
    }

    [Fact]
    public void ComputeMasteryWeakestFirst_ExcludesPitchesBelowMinimumAttempts()
    {
        var wellAttempted = new Pitch(NoteLetter.C, 0, 4);
        var barelyAttempted = new Pitch(NoteLetter.D, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(CreateSummary(
            DateTimeOffset.UtcNow,
            [
                new PitchAttemptSummary(wellAttempted, 2, SightReadingHistory.MinimumMasteryAttempts),
                new PitchAttemptSummary(barelyAttempted, 0, SightReadingHistory.MinimumMasteryAttempts - 1),
            ]));

        IReadOnlyList<PitchMastery> weakestFirst = history.ComputeMasteryWeakestFirst();

        PitchMastery onlyEntry = Assert.Single(weakestFirst);
        Assert.Equal(wellAttempted, onlyEntry.Pitch);
    }

    [Fact]
    public void ComputeMasteryWeakestFirst_OrdersWeakestAccuracyFirst()
    {
        var strongPitch = new Pitch(NoteLetter.C, 0, 4);
        var weakPitch = new Pitch(NoteLetter.D, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(CreateSummary(
            DateTimeOffset.UtcNow,
            [
                new PitchAttemptSummary(strongPitch, 5, 5),
                new PitchAttemptSummary(weakPitch, 1, 5),
            ]));

        IReadOnlyList<PitchMastery> weakestFirst = history.ComputeMasteryWeakestFirst();

        Assert.Equal([weakPitch, strongPitch], weakestFirst.Select(mastery => mastery.Pitch));
    }

    [Fact]
    public void ComputeMasteryWeakestFirst_TiedAccuracy_OrdersByPitchForDeterministicOutput()
    {
        var higherPitch = new Pitch(NoteLetter.G, 0, 4);
        var lowerPitch = new Pitch(NoteLetter.C, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(CreateSummary(
            DateTimeOffset.UtcNow,
            [
                new PitchAttemptSummary(higherPitch, 2, 5),
                new PitchAttemptSummary(lowerPitch, 2, 5),
            ]));

        IReadOnlyList<PitchMastery> weakestFirst = history.ComputeMasteryWeakestFirst();

        Assert.Equal([lowerPitch, higherPitch], weakestFirst.Select(mastery => mastery.Pitch));
    }

    private static SightReadingSessionSummary CreateSummary(
        DateTimeOffset completedAt,
        IReadOnlyList<PitchAttemptSummary>? pitchAttempts = null) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            completedAt,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            8,
            TimeSpan.FromSeconds(20),
            6,
            2,
            pitchAttempts ?? []);
}
