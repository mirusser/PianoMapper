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
            Guid.NewGuid(),
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
    public void Create_LateButRightPitchPrompt_KeepsPitchCorrectAndCountsTimingSeparately()
    {
        NoteReadingPromptResult[] results =
        [
            CreatePromptResult(0, NoteLetter.C, isFirstTryCorrect: true, wrongAttemptCount: 0),
            CreatePromptResult(1, NoteLetter.D, isFirstTryCorrect: false, wrongAttemptCount: 1) with
            {
                IsPitchFirstTryCorrect = true,
                OnsetVerdict = Verdict.Late,
            },
            CreatePromptResult(2, NoteLetter.E, isFirstTryCorrect: false, wrongAttemptCount: 1) with
            {
                IsPitchFirstTryCorrect = true,
                OnsetVerdict = Verdict.Early,
                DurationVerdict = Verdict.TooShort,
            },
            CreatePromptResult(3, NoteLetter.F, isFirstTryCorrect: false, wrongAttemptCount: 1) with
            {
                IsPitchFirstTryCorrect = true,
                OnsetVerdict = Verdict.Correct,
                DurationVerdict = Verdict.TooLong,
            },
            CreatePromptResult(4, NoteLetter.G, isFirstTryCorrect: false, wrongAttemptCount: 1) with
            {
                IsPitchFirstTryCorrect = false,
            },
        ];

        SightReadingSessionSummary summary = SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromSeconds(30),
            results);

        Assert.Equal(1, summary.FirstTryCorrectCount);
        Assert.Equal(4, summary.PitchFirstTryCorrectCount);
        Assert.Equal(3, summary.TimingMistakeCount);
        Assert.Equal(1, summary.EarlyCount);
        Assert.Equal(1, summary.LateCount);
        Assert.Equal(1, summary.TooShortCount);
        Assert.Equal(1, summary.TooLongCount);
        PitchAttemptSummary lateAttempt = Assert.Single(
            summary.PitchAttempts,
            attempt => attempt.Pitch == new Pitch(NoteLetter.D, 0, 4));
        Assert.Equal(1, lateAttempt.CorrectFirstTryCount);
        PitchAttemptSummary wrongAttempt = Assert.Single(
            summary.PitchAttempts,
            attempt => attempt.Pitch == new Pitch(NoteLetter.G, 0, 4));
        Assert.Equal(0, wrongAttempt.CorrectFirstTryCount);
    }

    [Fact]
    public void Create_PitchOnlyResults_HaveZeroTimingCounts()
    {
        SightReadingSessionSummary summary = SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.FromSeconds(30),
            [CreatePromptResult(0, NoteLetter.C, isFirstTryCorrect: false, wrongAttemptCount: 1)]);

        Assert.Equal(0, summary.PitchFirstTryCorrectCount);
        Assert.Equal(0, summary.TimingMistakeCount);
        Assert.Equal(0, summary.EarlyCount);
        Assert.Equal(0, summary.LateCount);
    }

    [Fact]
    public void Create_PromptResults_CapturesStaffAttemptsAndResponseTimesPerNote()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        var d4 = new Pitch(NoteLetter.D, 0, 4);
        NoteReadingPromptResult[] results =
        [
            NotePrompt(0, c4, Staff.Treble, pitchCorrect: true, responseMilliseconds: null),
            NotePrompt(1, d4, Staff.Treble, pitchCorrect: false, responseMilliseconds: 1200),
            NotePrompt(2, c4, Staff.Bass, pitchCorrect: true, responseMilliseconds: 800),
            NotePrompt(3, c4, Staff.Treble, pitchCorrect: true, responseMilliseconds: 1000),
        ];

        SightReadingSessionSummary summary = Create(results);

        IReadOnlyList<NoteAttemptSummary> attempts = Assert.IsAssignableFrom<IReadOnlyList<NoteAttemptSummary>>(summary.NoteAttempts);
        Assert.Equal(3, attempts.Count);
        NoteAttemptSummary trebleC4 = Assert.Single(attempts, attempt => attempt.Pitch == c4 && attempt.Staff == Staff.Treble);
        Assert.Equal(2, trebleC4.AttemptCount);
        Assert.Equal(2, trebleC4.PitchFirstTryCorrectCount);
        Assert.Equal(1, trebleC4.ResponseSampleCount);
        Assert.Equal(1000, trebleC4.TotalResponseMilliseconds);
        NoteAttemptSummary bassC4 = Assert.Single(attempts, attempt => attempt.Pitch == c4 && attempt.Staff == Staff.Bass);
        Assert.Equal(1, bassC4.AttemptCount);
        Assert.Equal(800, bassC4.TotalResponseMilliseconds);
        NoteAttemptSummary trebleD4 = Assert.Single(attempts, attempt => attempt.Pitch == d4);
        Assert.Equal(0, trebleD4.PitchFirstTryCorrectCount);
        Assert.Equal(1200, trebleD4.TotalResponseMilliseconds);
        // The older per-pitch list stays populated, merging both staves.
        Assert.Equal(3, Assert.Single(summary.PitchAttempts, attempt => attempt.Pitch == c4).AttemptCount);
    }

    [Fact]
    public void Create_OnsetGradedResultsWithoutResponseTimes_HaveNullResponseTotals()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        NoteReadingPromptResult[] results =
        [
            NotePrompt(0, c4, Staff.Treble, pitchCorrect: true, responseMilliseconds: null),
            NotePrompt(1, c4, Staff.Treble, pitchCorrect: true, responseMilliseconds: null),
        ];

        NoteAttemptSummary attempt = Assert.Single(Create(results).NoteAttempts!);

        Assert.Equal(2, attempt.AttemptCount);
        Assert.Equal(0, attempt.ResponseSampleCount);
        Assert.Null(attempt.TotalResponseMilliseconds);
    }

    [Fact]
    public void Create_ChordPrompt_GivesEveryMemberItsOwnStaffAndTheSharedResponseTime()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        var e3 = new Pitch(NoteLetter.E, 0, 3);
        ScoreNote[] notes =
        [
            new ScoreNote(c4, new NoteValue(4), 0, 0, Staff.Treble),
            new ScoreNote(e3, new NoteValue(4), 0, 0, Staff.Bass),
        ];
        var chord = new NoteReadingPromptResult(
            0,
            0,
            [.. notes],
            [c4, e3],
            [],
            0,
            IsFirstTryCorrect: true,
            IsComplete: true,
            CompletedAt: null)
        {
            ResponseTime = TimeSpan.FromMilliseconds(900),
        };

        IReadOnlyList<NoteAttemptSummary> attempts = Create([chord]).NoteAttempts!;

        Assert.Equal(Staff.Treble, Assert.Single(attempts, attempt => attempt.Pitch == c4).Staff);
        Assert.Equal(Staff.Bass, Assert.Single(attempts, attempt => attempt.Pitch == e3).Staff);
        Assert.All(attempts, attempt => Assert.Equal(900, attempt.TotalResponseMilliseconds));
    }

    [Fact]
    public void Create_WrongKeys_AreAggregatedIntoConfusionsAgainstTheExpectedNote()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        var d4 = new Pitch(NoteLetter.D, 0, 4);
        var b3 = new Pitch(NoteLetter.B, 0, 3);
        NoteReadingPromptResult[] results =
        [
            NotePrompt(0, c4, Staff.Treble, pitchCorrect: false, responseMilliseconds: null, wrongKeys: [d4, d4]),
            NotePrompt(1, c4, Staff.Treble, pitchCorrect: false, responseMilliseconds: null, wrongKeys: [d4, b3]),
            NotePrompt(2, c4, Staff.Bass, pitchCorrect: false, responseMilliseconds: null, wrongKeys: [d4]),
        ];

        IReadOnlyList<ConfusionSummary> confusions = Create(results).Confusions!;

        ConfusionSummary trebleStep = Assert.Single(
            confusions,
            confusion => confusion.Expected == c4 && confusion.Played == d4 && confusion.Staff == Staff.Treble);
        Assert.Equal(3, trebleStep.Count);
        Assert.Equal(1, Assert.Single(confusions, confusion => confusion.Played == b3).Count);
        Assert.Equal(1, Assert.Single(confusions, confusion => confusion.Staff == Staff.Bass).Count);
        // Most frequent first.
        Assert.Equal(trebleStep, confusions[0]);
    }

    [Fact]
    public void Create_ManyDistinctConfusions_KeepsOnlyTheTwentyMostFrequent()
    {
        var expected = new Pitch(NoteLetter.C, 0, 4);
        Pitch[] wrongKeys = Enumerable.Range(0, 30)
            .Select(index => new Pitch(NoteLetter.C, 0, 5).MidiNumber + index)
            .Select(midi => PitchFromMidi(midi))
            .ToArray();
        NoteReadingPromptResult[] results =
        [
            NotePrompt(0, expected, Staff.Treble, pitchCorrect: false, responseMilliseconds: null, wrongKeys: wrongKeys),
            NotePrompt(1, expected, Staff.Treble, pitchCorrect: false, responseMilliseconds: null, wrongKeys: [wrongKeys[29]]),
        ];

        IReadOnlyList<ConfusionSummary> confusions = Create(results).Confusions!;

        Assert.Equal(SightReadingSessionSummary.MaximumConfusions, confusions.Count);
        Assert.Equal(20, SightReadingSessionSummary.MaximumConfusions);
        Assert.Equal(wrongKeys[29], confusions[0].Played);
        Assert.Equal(2, confusions[0].Count);
    }

    [Fact]
    public void Create_CleanSession_HasNoConfusions()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);

        SightReadingSessionSummary summary = Create([NotePrompt(0, c4, Staff.Treble, pitchCorrect: true, responseMilliseconds: 500)]);

        Assert.Empty(summary.Confusions!);
    }

    [Fact]
    public void Json_WorstCaseSession_StaysFarBelowTheStorageLimitForAFullHistory()
    {
        // The worst case: a 16-prompt chord exercise of all-distinct pitches with 20 distinct confusions. It measures
        // about 1.5 MB for a full 100-session history; a typical single-note session is roughly a tenth of that.
        // Browsers allow about 5 MB per origin, so the bound below keeps history under 40% of it.
        NoteReadingPromptResult[] results = Enumerable.Range(0, 16)
            .Select(index =>
            {
                Pitch[] chord =
                [
                    PitchFromMidi(36 + index),
                    PitchFromMidi(52 + index),
                    PitchFromMidi(68 + index),
                ];
                return new NoteReadingPromptResult(
                    index,
                    index,
                    [.. chord.Select((pitch, staffIndex) => new ScoreNote(
                        pitch,
                        new NoteValue(4),
                        index,
                        index,
                        staffIndex == 0 ? Staff.Bass : Staff.Treble))],
                    [.. chord],
                    [PitchFromMidi(30 + index), PitchFromMidi(31 + index)],
                    2,
                    IsFirstTryCorrect: false,
                    IsComplete: true,
                    CompletedAt: null)
                {
                    IsPitchFirstTryCorrect = false,
                    ResponseTime = TimeSpan.FromMilliseconds(1000 + index),
                };
            })
            .ToArray();
        SightReadingSessionSummary summary = Create(results);
        SightReadingHistory history = SightReadingHistory.Empty;
        for (int index = 0; index < 100; index++)
        {
            history = history.WithCompletedSession(summary with
            {
                SessionId = Guid.NewGuid(),
                CompletedAt = summary.CompletedAt.AddMinutes(index),
            });
        }

        int bytes = System.Text.Encoding.UTF8.GetByteCount(history.ToJson());

        Assert.True(bytes < 2_000_000, $"100 worst-case sessions serialized to {bytes} bytes.");
    }

    [Fact]
    public void Create_AccidentalsExercise_KeepsEveryPitchsSpellingThroughTheStoredHistory()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.Accidentals,
                PromptCount: 16,
                NoteReadingMode.PitchAndOrder),
            new Random(2));
        var session = new NoteReadingSession();
        session.Reset(score);
        Pitch[] composed = score.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch).ToArray();
        foreach (Pitch pitch in composed)
        {
            session.Check(pitch);
        }

        SightReadingSessionSummary summary = SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            nameof(SightReadingPresetId.Accidentals),
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            session.ElapsedTime,
            session.PromptResults);
        SightReadingSessionSummary stored = Assert.Single(
            SightReadingHistory.FromJson(SightReadingHistory.Empty.WithCompletedSession(summary).ToJson()).Entries);

        Assert.Contains(composed, pitch => pitch.Alter > 0);
        Assert.Contains(composed, pitch => pitch.Alter < 0);
        Assert.Equal(
            composed.Distinct().OrderBy(pitch => pitch.MidiNumber).ThenBy(pitch => pitch.Alter),
            stored.PitchAttempts.Select(attempt => attempt.Pitch).OrderBy(pitch => pitch.MidiNumber).ThenBy(pitch => pitch.Alter));
        Assert.Equal(
            composed.Distinct().OrderBy(pitch => pitch.MidiNumber).ThenBy(pitch => pitch.Alter),
            stored.NoteAttempts!.Select(attempt => attempt.Pitch).OrderBy(pitch => pitch.MidiNumber).ThenBy(pitch => pitch.Alter));
    }

    [Fact]
    public void Create_NullOrWhitespacePresetId_Throws()
    {
        Assert.Throws<ArgumentException>(() => SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            " ",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.Zero,
            []));
    }

    [Fact]
    public void Create_SessionId_IsKeptAsSuppliedByTheCaller()
    {
        var sessionId = Guid.Parse("5b0c1d2e-3f40-4a51-8b62-7c8d9e0f1a2b");

        SightReadingSessionSummary summary = SightReadingSessionSummary.Create(
            sessionId,
            DateTimeOffset.UtcNow,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.Zero,
            []);

        Assert.Equal(sessionId, summary.SessionId);
        Assert.Equal(3, summary.SchemaVersion);
    }

    [Fact]
    public void Create_EmptySessionId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingSessionSummary.Create(
            Guid.Empty,
            DateTimeOffset.UtcNow,
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.Zero,
            []));
    }

    private static SightReadingSessionSummary Create(IReadOnlyList<NoteReadingPromptResult> results) =>
        SightReadingSessionSummary.Create(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            TimeSpan.FromSeconds(30),
            results);

    private static NoteReadingPromptResult NotePrompt(
        int promptIndex,
        Pitch pitch,
        Staff staff,
        bool pitchCorrect,
        int? responseMilliseconds,
        Pitch[]? wrongKeys = null) =>
        new(
            promptIndex,
            promptIndex,
            [new ScoreNote(pitch, new NoteValue(4), promptIndex, promptIndex, staff)],
            [pitch],
            [.. wrongKeys ?? []],
            wrongKeys?.Length ?? 0,
            IsFirstTryCorrect: pitchCorrect,
            IsComplete: true,
            CompletedAt: null)
        {
            IsPitchFirstTryCorrect = pitchCorrect,
            ResponseTime = responseMilliseconds is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null,
        };

    private static Pitch PitchFromMidi(int midi)
    {
        int[] semitoneToLetter = [0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6];
        int[] semitoneToAlter = [0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0];
        int octave = (midi / 12) - 1;
        int semitone = midi % 12;
        return new Pitch((NoteLetter)semitoneToLetter[semitone], semitoneToAlter[semitone], octave);
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
