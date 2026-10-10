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
            Guid.NewGuid(),
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
        Assert.Equal(original.SessionId, roundTripped.SessionId);
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

    [Fact]
    public void FromJson_RealV1Payload_LoadsEveryEntryWithoutLosingData()
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v1.json"));

        SightReadingHistory history = SightReadingHistory.FromJson(json);

        Assert.Equal(3, history.Entries.Count);
        Assert.All(history.Entries, entry => Assert.Equal(1, entry.SchemaVersion));
        Assert.Equal(
            [NoteReadingMode.PitchHoldAndRhythm, NoteReadingMode.PitchAndHold, NoteReadingMode.PitchAndOrder],
            history.Entries.Select(entry => entry.Mode));
        Assert.All(history.Entries, entry => Assert.Equal(8, entry.PromptCount));
        Assert.All(history.Entries, entry => Assert.NotEmpty(entry.PitchAttempts));
        Assert.All(history.Entries, entry => Assert.Null(entry.PitchFirstTryCorrectCount));
        Assert.All(history.Entries, entry => Assert.Null(entry.TimingMistakeCount));
    }

    [Fact]
    public void ComputeMastery_V1PitchAndOrderEntry_ContributesExactlyAsBefore()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(
            CreateSummary(DateTimeOffset.UtcNow, [new PitchAttemptSummary(c4, 2, 3)]) with { SchemaVersion = 1 });

        PitchMastery mastery = Assert.Single(history.ComputeMastery());

        Assert.Equal(c4, mastery.Pitch);
        Assert.Equal(2, mastery.CorrectFirstTryCount);
        Assert.Equal(3, mastery.AttemptCount);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndHold)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm)]
    public void ComputeMastery_V1HoldOrRhythmEntry_StaysInEntriesButIsExcludedFromMastery(NoteReadingMode mode)
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingSessionSummary legacyEntry = CreateSummary(
            DateTimeOffset.UtcNow,
            [new PitchAttemptSummary(c4, 0, 5)]) with
        {
            SchemaVersion = 1,
            Mode = mode,
        };
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(legacyEntry);

        Assert.Single(history.Entries);
        Assert.Empty(history.ComputeMastery());
        Assert.Empty(history.ComputeMasteryWeakestFirst());
    }

    [Fact]
    public void ComputeMastery_V1FixtureHoldAndRhythmEntries_DoNotAffectPitchCounts()
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v1.json"));
        SightReadingHistory history = SightReadingHistory.FromJson(json);

        IReadOnlyList<PitchMastery> mastery = history.ComputeMastery();

        // G4 appears in the v1 pitch-only session (1 of 1) and the v1 rhythm session (1 of 2, excluded).
        PitchMastery g4 = Assert.Single(mastery, entry => entry.Pitch == new Pitch(NoteLetter.G, 0, 4));
        Assert.Equal(1, g4.CorrectFirstTryCount);
        Assert.Equal(1, g4.AttemptCount);
        Assert.DoesNotContain(mastery, entry => entry.Pitch == new Pitch(NoteLetter.D, 0, 3));
    }

    [Fact]
    public void ComputeMastery_V2LateButRightPitchPrompt_CountsAsPitchCorrect()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingSessionSummary summary = SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromSeconds(10),
            [
                new NoteReadingPromptResult(
                    0,
                    0,
                    [new ScoreNote(c4, new NoteValue(4), 0, 0, Staff.Treble)],
                    [c4],
                    [],
                    1,
                    IsFirstTryCorrect: false,
                    IsComplete: true,
                    CompletedAt: TimeSpan.FromSeconds(1))
                {
                    IsPitchFirstTryCorrect = true,
                    OnsetVerdict = Verdict.Late,
                },
            ]);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(summary);

        PitchMastery mastery = Assert.Single(history.ComputeMastery());

        Assert.Equal(1, mastery.CorrectFirstTryCount);
        Assert.Equal(1, mastery.AttemptCount);
    }

    [Fact]
    public void ToJson_V2OptionalMembers_RoundTripLosslessly()
    {
        SightReadingSessionSummary original = CreateSummary(DateTimeOffset.UtcNow) with
        {
            RhythmPreset = "Basic",
            IsGrandStaff = true,
            TempoBeatsPerMinute = 60,
            Pacing = "playAlong",
            PitchFirstTryCorrectCount = 7,
            TimingMistakeCount = 3,
            EarlyCount = 1,
            LateCount = 2,
            TooShortCount = 1,
            TooLongCount = 0,
        };

        SightReadingHistory reloaded = SightReadingHistory.FromJson(
            SightReadingHistory.Empty.WithCompletedSession(original).ToJson());

        SightReadingSessionSummary roundTripped = Assert.Single(reloaded.Entries);
        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(original.RhythmPreset, roundTripped.RhythmPreset);
        Assert.Equal(original.IsGrandStaff, roundTripped.IsGrandStaff);
        Assert.Equal(original.TempoBeatsPerMinute, roundTripped.TempoBeatsPerMinute);
        Assert.Equal(original.Pacing, roundTripped.Pacing);
        Assert.Equal(original.PitchFirstTryCorrectCount, roundTripped.PitchFirstTryCorrectCount);
        Assert.Equal(original.TimingMistakeCount, roundTripped.TimingMistakeCount);
        Assert.Equal(original.EarlyCount, roundTripped.EarlyCount);
        Assert.Equal(original.LateCount, roundTripped.LateCount);
        Assert.Equal(original.TooShortCount, roundTripped.TooShortCount);
        Assert.Equal(original.TooLongCount, roundTripped.TooLongCount);
    }

    [Fact]
    public void ToJson_Motion_RoundTripsAndEntriesWithoutItParseWithNull()
    {
        SightReadingSessionSummary withMotion = CreateSummary(DateTimeOffset.UtcNow) with { Motion = "Melodic" };
        SightReadingSessionSummary withoutMotion = CreateSummary(DateTimeOffset.UtcNow.AddMinutes(1));

        SightReadingHistory reloaded = SightReadingHistory.FromJson(
            SightReadingHistory.Empty.WithCompletedSession(withMotion).WithCompletedSession(withoutMotion).ToJson());

        Assert.Equal("Melodic", reloaded.Entries.Single(entry => entry.CompletedAt == withMotion.CompletedAt).Motion);
        Assert.Null(reloaded.Entries.Single(entry => entry.CompletedAt == withoutMotion.CompletedAt).Motion);
    }

    [Fact]
    public void ToJson_NoteAttemptsAndConfusions_RoundTripLosslesslyAndStorePitchesCompactly()
    {
        var expected = new Pitch(NoteLetter.C, 1, 4);
        var played = new Pitch(NoteLetter.D, 0, 4);
        SightReadingSessionSummary original = SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "FiveNote",
            Staff.Bass,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.FromSeconds(12),
            [
                new NoteReadingPromptResult(
                    0,
                    0,
                    [new ScoreNote(expected, new NoteValue(4), 0, 0, Staff.Bass)],
                    [expected],
                    [played, played],
                    2,
                    IsFirstTryCorrect: false,
                    IsComplete: true,
                    CompletedAt: null)
                {
                    IsPitchFirstTryCorrect = false,
                    ResponseTime = TimeSpan.FromMilliseconds(1500),
                },
            ]);

        string json = SightReadingHistory.Empty.WithCompletedSession(original).ToJson();
        SightReadingSessionSummary roundTripped = Assert.Single(SightReadingHistory.FromJson(json).Entries);

        Assert.Equal(original.NoteAttempts, roundTripped.NoteAttempts);
        Assert.Equal(original.Confusions, roundTripped.Confusions);
        Assert.Equal(original.PitchAttempts, roundTripped.PitchAttempts);
        // The derived members of a pitch (MIDI number, frequency, staff position) are not stored.
        Assert.DoesNotContain("frequency", json);
        Assert.DoesNotContain("midiNumber", json);
    }

    [Fact]
    public void FromJson_V2EntryWithoutOptionalMembers_ParsesWithNullDefaults()
    {
        string json = SightReadingHistory.Empty.WithCompletedSession(CreateSummary(DateTimeOffset.UtcNow)).ToJson();

        SightReadingSessionSummary entry = Assert.Single(SightReadingHistory.FromJson(json).Entries);

        Assert.Equal(SightReadingSessionSummary.CurrentSchemaVersion, entry.SchemaVersion);
        Assert.Null(entry.RhythmPreset);
        Assert.Null(entry.IsGrandStaff);
        Assert.Null(entry.TempoBeatsPerMinute);
        Assert.Null(entry.Pacing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromJson_NonPositiveSchemaVersion_SkipsThatEntry(int schemaVersion)
    {
        SightReadingSessionSummary invalidEntry =
            CreateSummary(DateTimeOffset.UtcNow) with { SchemaVersion = schemaVersion };
        string json = SightReadingHistory.Empty.WithCompletedSession(invalidEntry).ToJson();

        Assert.Empty(SightReadingHistory.FromJson(json).Entries);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(80, 0.2)]
    [InlineData(0, 1)]
    public void CalculateWeakness_WithoutSpeedData_IsTheInaccuracyFraction(double accuracyPercent, double expected)
    {
        Assert.Equal(expected, NoteMastery.CalculateWeakness(accuracyPercent, null, null), precision: 9);
    }

    [Theory]
    [InlineData("sight-reading-history-v1.json", 3)]
    [InlineData("sight-reading-history-v2.json", 2)]
    [InlineData("sight-reading-history-v3.json", 2)]
    public void FromJson_RealPayloadOfEveryShippedSchema_LoadsEveryEntryAndKeepsItsIdentityThroughASave(
        string fixture,
        int expectedEntryCount)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));

        SightReadingHistory loaded = SightReadingHistory.FromJson(json);
        SightReadingHistory saved = SightReadingHistory.FromJson(loaded.ToJson());

        Assert.Equal(expectedEntryCount, loaded.Entries.Count);
        Assert.Equal(
            loaded.Entries.Select(entry => (entry.SchemaVersion, entry.SessionId)),
            saved.Entries.Select(entry => (entry.SchemaVersion, entry.SessionId)));
    }

    [Fact]
    public void CalculateWeakness_LowerAccuracy_IsAlwaysWeaker()
    {
        TimeSpan typical = TimeSpan.FromSeconds(1);

        double[] scores = new double[] { 100, 90, 70, 40, 0 }
            .Select(accuracy => NoteMastery.CalculateWeakness(accuracy, typical, typical))
            .ToArray();

        Assert.Equal(scores.OrderBy(score => score), scores);
        Assert.Equal(scores.Length, scores.Distinct().Count());
    }

    [Fact]
    public void CalculateWeakness_SlowerThanTheLearnersOwnMedian_IsWeakerButNeverFasterBelowTheInaccuracy()
    {
        TimeSpan own = TimeSpan.FromSeconds(1);

        double fast = NoteMastery.CalculateWeakness(90, TimeSpan.FromMilliseconds(400), own);
        double typical = NoteMastery.CalculateWeakness(90, own, own);
        double slow = NoteMastery.CalculateWeakness(90, TimeSpan.FromSeconds(2), own);
        double verySlow = NoteMastery.CalculateWeakness(90, TimeSpan.FromSeconds(60), own);

        Assert.Equal(typical, fast, precision: 9);
        Assert.True(slow > typical);
        Assert.True(verySlow >= slow);
        Assert.True(verySlow <= typical + NoteMastery.MaximumSlownessWeight + 1e-9);
    }

    [Fact]
    public void ComputeNoteMastery_SameNoteOnTwoStaves_IsKeptApartAndTheBassWeaknessRanksFirst()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(CreateSummary(
            DateTimeOffset.UtcNow,
            [new PitchAttemptSummary(c4, 5, 10)]) with
        {
            NoteAttempts =
            [
                new NoteAttemptSummary(c4, Staff.Treble, 5, 5, 0, null),
                new NoteAttemptSummary(c4, Staff.Bass, 5, 0, 0, null),
            ],
        });

        IReadOnlyList<NoteMastery> weakestFirst = history.ComputeNoteMasteryWeakestFirst();

        Assert.Equal([Staff.Bass, Staff.Treble], weakestFirst.Select(mastery => mastery.Staff));
        Assert.Equal(0, weakestFirst[0].CorrectFirstTryCount);
        Assert.Equal(5, weakestFirst[1].CorrectFirstTryCount);
        Assert.True(weakestFirst[0].WeaknessScore > weakestFirst[1].WeaknessScore);
    }

    [Fact]
    public void ComputeNoteMastery_BelowTheMinimumAttempts_IsExcludedFromTheWeakestFirstList()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(CreateSummary(
            DateTimeOffset.UtcNow,
            [new PitchAttemptSummary(c4, 0, SightReadingHistory.MinimumMasteryAttempts - 1)]) with
        {
            NoteAttempts = [new NoteAttemptSummary(c4, Staff.Treble, SightReadingHistory.MinimumMasteryAttempts - 1, 0, 0, null)],
        });

        Assert.Single(history.ComputeNoteMastery());
        Assert.Empty(history.ComputeNoteMasteryWeakestFirst());
    }

    [Fact]
    public void ComputeNoteMasteryWeakestFirst_V1OnlyHistory_OrdersPitchesLikeTheOldPitchMastery()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v1.json"));
        SightReadingHistory history = SightReadingHistory.FromJson(json);
        // Make every pitch pass the attempt threshold so the whole ordering is compared.
        SightReadingHistory padded = SightReadingHistory.Empty;
        for (int copy = 0; copy < SightReadingHistory.MinimumMasteryAttempts; copy++)
        {
            foreach (SightReadingSessionSummary entry in history.Entries)
            {
                padded = padded.WithCompletedSession(entry with { CompletedAt = entry.CompletedAt.AddDays(copy * 10) });
            }
        }

        Pitch[] oldOrder = padded.ComputeMasteryWeakestFirst().Select(mastery => mastery.Pitch).ToArray();
        Pitch[] newOrder = padded.ComputeNoteMasteryWeakestFirst().Select(mastery => mastery.Pitch).ToArray();

        Assert.NotEmpty(oldOrder);
        Assert.Equal(oldOrder, newOrder);
        Assert.All(padded.ComputeNoteMastery(), mastery => Assert.Null(mastery.Staff));
    }

    [Fact]
    public void GetNoteMastery_StaffAgnosticLegacyData_IsMergedIntoEachStaffAndUsedWhenNothingMoreSpecificExists()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingSessionSummary legacy = CreateSummary(
            DateTimeOffset.UtcNow,
            [new PitchAttemptSummary(c4, 5, 5)]) with
        {
            SchemaVersion = 1,
        };
        SightReadingSessionSummary bassSession = CreateSummary(
            DateTimeOffset.UtcNow.AddMinutes(1),
            [new PitchAttemptSummary(c4, 0, 5)]) with
        {
            NoteAttempts = [new NoteAttemptSummary(c4, Staff.Bass, 5, 0, 0, null)],
        };
        SightReadingHistory history = SightReadingHistory.Empty
            .WithCompletedSession(legacy)
            .WithCompletedSession(bassSession);

        NoteMastery bass = Assert.IsType<NoteMastery>(history.GetNoteMastery(c4, Staff.Bass));
        NoteMastery treble = Assert.IsType<NoteMastery>(history.GetNoteMastery(c4, Staff.Treble));

        Assert.Equal(10, bass.AttemptCount);
        Assert.Equal(5, bass.CorrectFirstTryCount);
        Assert.Equal(5, treble.AttemptCount);
        Assert.Equal(5, treble.CorrectFirstTryCount);
        Assert.Null(history.GetNoteMastery(new Pitch(NoteLetter.G, 0, 5), Staff.Treble));
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndHold)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm)]
    public void ComputeNoteMastery_LegacyConflatedEntries_StayExcluded(NoteReadingMode mode)
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        SightReadingHistory history = SightReadingHistory.Empty.WithCompletedSession(
            CreateSummary(
                DateTimeOffset.UtcNow,
                [new PitchAttemptSummary(c4, 0, 9)]) with
            {
                SchemaVersion = 1,
                Mode = mode,
            });

        Assert.Empty(history.ComputeNoteMastery());
    }

    [Fact]
    public void ComputeNoteMastery_ResponseTimes_UseTheMedianOfSessionAveragesAndTheLearnersOwnMedianToRankSlowness()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        var d4 = new Pitch(NoteLetter.D, 0, 4);
        static SightReadingSessionSummary Session(DateTimeOffset at, Pitch pitch, int totalMs) =>
            CreateSummary(at, [new PitchAttemptSummary(pitch, 5, 5)]) with
            {
                NoteAttempts = [new NoteAttemptSummary(pitch, Staff.Treble, 5, 5, 5, totalMs)],
            };
        DateTimeOffset start = DateTimeOffset.UtcNow;
        // C4 averages 1.0 s, 1.0 s and 3.0 s per session (median 1.0 s); D4 averages 3.0 s in every session.
        SightReadingHistory history = SightReadingHistory.Empty
            .WithCompletedSession(Session(start, c4, 5000))
            .WithCompletedSession(Session(start.AddMinutes(1), c4, 5000))
            .WithCompletedSession(Session(start.AddMinutes(2), c4, 15000))
            .WithCompletedSession(Session(start.AddMinutes(3), d4, 15000))
            .WithCompletedSession(Session(start.AddMinutes(4), d4, 15000))
            .WithCompletedSession(Session(start.AddMinutes(5), d4, 15000));

        IReadOnlyList<NoteMastery> weakestFirst = history.ComputeNoteMasteryWeakestFirst();

        Assert.Equal(TimeSpan.FromSeconds(1), weakestFirst.Single(mastery => mastery.Pitch == c4).MedianResponseTime);
        Assert.Equal(TimeSpan.FromSeconds(3), weakestFirst.Single(mastery => mastery.Pitch == d4).MedianResponseTime);
        // Equally accurate, but D4 is slower than the learner's own typical time, so it is the weaker note.
        Assert.Equal([d4, c4], weakestFirst.Select(mastery => mastery.Pitch));
    }

    private static SightReadingSessionSummary CreateSummary(
        DateTimeOffset completedAt,
        IReadOnlyList<PitchAttemptSummary>? pitchAttempts = null) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            Guid.NewGuid(),
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
