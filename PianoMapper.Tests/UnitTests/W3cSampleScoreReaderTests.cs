using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

/// <summary>
/// Pins what <see cref="MusicXmlScoreReader"/> does with each of the 18 official W3C MusicXML sample files in
/// <c>Fixtures/w3c</c> (provenance in that folder's README). A sample in this app's scope (one part or one part per
/// hand, up to two staves, pitched piano notation) imports; a sample outside it fails with an intentional, readable
/// error. Every file has a row; none is skipped.
/// </summary>
public sealed partial class W3cSampleScoreReaderTests
{
    private sealed record Outcome(Type? ExceptionType, string? MessageFragment, Action<MusicXmlReadResult>? Verify);

    private static Outcome Imports(Action<MusicXmlReadResult> verify) => new(null, null, verify);

    private static Outcome Fails<TException>(string messageFragment)
        where TException : Exception =>
        new(typeof(TException), messageFragment, null);

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "w3c");

    private static int WarningCount(MusicXmlReadResult result, string construct) =>
        result.Warnings.SingleOrDefault(warning => warning.Construct == construct)?.Count ?? 0;

    private static IEnumerable<ScoreNote> NotesOf(MusicXmlReadResult result) =>
        result.Score.Measures.SelectMany(measure => measure.Notes);

    private static IEnumerable<ScoreRest> RestsOf(MusicXmlReadResult result) =>
        result.Score.Measures.SelectMany(measure => measure.Rests);

    // beams-ties.invalid differs from beams-ties.musicxml only in <beam fan>, which the reader never looks at, so both
    // read the same.
    private static void VerifyThirtySecondNoteScale(MusicXmlReadResult result)
    {
        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(32, measure.Notes.Count);
        Assert.All(measure.Notes, note => Assert.Equal(new NoteValue(32), note.NoteValue));
        Assert.Equal(7 * 0.125, measure.Notes[7].BeatOffset, 12);
        Assert.Equal(BeamState.Begin, measure.Notes[0].BeamState);
        Assert.Empty(result.Warnings);
    }

    private static readonly Dictionary<string, Outcome> Outcomes = new(StringComparer.Ordinal)
    {
        // A fragment with no <divisions>, read as one division per quarter note: two chords of four notes, and the
        // microtonal accidental signs beside two of the notes are reported, not drawn.
        ["accidental-element-multiple.musicxml"] = Imports(result =>
        {
            var measure = Assert.Single(result.Score.Measures);
            Assert.Equal(8, measure.Notes.Count);
            Assert.Equal(6, measure.Notes.Count(note => note.IsChordContinuation));
            Assert.Equal(3, measure.Notes.Count(note => note.Accidental == ScoreAccidental.Flat));
            Assert.Equal(1, WarningCount(result, "missing divisions"));
            Assert.Equal(4, WarningCount(result, "microtonal or special accidental"));
        }),

        // Starts with a non-traditional (microtonal) key signature that the same measure replaces with C major.
        ["accidentals.musicxml"] = Imports(result =>
        {
            Assert.Equal(7, result.Score.Measures.Count);
            Assert.Equal(55, NotesOf(result).Count());
            Assert.Equal(0, result.Score.KeyFifths);
            Assert.Equal(new Tempo(100), result.Score.Tempo);
            Assert.Empty(RestsOf(result));
            Assert.Equal(1, WarningCount(result, "hidden rest"));
            Assert.Equal(1, WarningCount(result, "non-traditional key"));
            Assert.Equal(49, WarningCount(result, "lyric"));
        }),

        // Five whole-measure rests without a <type>, three of them closed by one, two and three coda signs.
        ["barline-multiple-coda.musicxml"] = Imports(result =>
        {
            Assert.Equal(5, result.Score.Measures.Count);
            Assert.Empty(NotesOf(result));
            Assert.All(RestsOf(result), rest => Assert.True(rest.IsMeasureRest));
            Assert.Equal(
                [(ScoreBarlineMark.Coda, 1), (ScoreBarlineMark.Coda, 2), (ScoreBarlineMark.Coda, 3)],
                result.Score.Measures.Take(3).Select(measure => (measure.RightBarline!.Mark!.Value, measure.RightBarline.MarkCount)));
            Assert.Null(result.Score.Measures[3].RightBarline);
        }),

        ["beams-ties.musicxml"] = Imports(VerifyThirtySecondNoteScale),
        ["beams-ties.invalid.musicxml"] = Imports(VerifyThirtySecondNoteScale),

        // A guitar artificial harmonic sounds another pitch than the written one, so it is not imported.
        ["harmonic-element.musicxml"] = Fails<NotSupportedException>("<harmonic>"),

        // One part of four staves (two braced pairs): beyond the two-staff grand staff. The .invalid variant only moves the
        // part-symbol staff numbers out of range, which is never reached because <staves> already stops the import.
        ["parts-groups.musicxml"] = Fails<NotSupportedException>("<staves>"),
        ["parts-groups.invalid.musicxml"] = Fails<NotSupportedException>("<staves>"),

        // Fourteen whole-measure rests carrying every repeat construct: forward/backward repeats with counts, voltas,
        // segno, coda and to-coda marks, D.C. / D.S. / Fine. The jumps are reported because playback is linear.
        ["repeats-jumps.musicxml"] = Imports(result =>
        {
            Assert.Equal(14, result.Score.Measures.Count);
            Assert.All(RestsOf(result), rest => Assert.True(rest.IsMeasureRest));
            var backward = result.Score.Measures
                .Where(measure => measure.RightBarline?.Repeat == ScoreRepeatDirection.Backward)
                .Select(measure => measure.RightBarline!.RepeatTimes)
                .ToArray();
            Assert.Equal([3, 3, 4, 5, 4], backward);
            Assert.Equal(
                2,
                result.Score.Measures.Count(measure => measure.LeftBarline?.Repeat == ScoreRepeatDirection.Forward));
            var endings = result.Score.Measures
                .SelectMany(measure => new[] { measure.LeftBarline?.Ending, measure.RightBarline?.Ending })
                .OfType<ScoreEnding>()
                .ToArray();
            Assert.Equal(5, endings.Count(ending => ending.Type == ScoreEndingType.Start));
            Assert.Equal(4, endings.Count(ending => ending.Type == ScoreEndingType.Stop));
            Assert.Equal(1, endings.Count(ending => ending.Type == ScoreEndingType.Discontinue));
            Assert.Equal(4, WarningCount(result, "jump instruction"));
        }),

        // W3C's invalid variant: a barline holding a segno together with a coda, and a D.S. to a segno that is not
        // defined. The reader refuses the file at the first of them rather than importing a malformed score.
        ["repeats-jumps.invalid.musicxml"] = Fails<InvalidDataException>("<segno>"),

        // A fragment: its durations are written for a divisions value that the file never states, so they cannot be
        // read. This is a readable error naming <divisions>, not a guess at the missing value.
        ["rest-and-display-step-elements.musicxml"] = Fails<InvalidDataException>("<divisions>"),

        // A voice part plus a two-staff piano part: three staves, beyond one grand staff.
        ["tutorial-apres-un-reve.musicxml"] = Fails<NotSupportedException>("<part>"),

        ["tutorial-chopin-prelude.musicxml"] = Imports(result =>
        {
            Assert.Equal(-3, result.Score.KeyFifths);
            Assert.Equal(new TimeSignature(4, new NoteValue(4)), result.Score.TimeSignature);
            Assert.Equal(new Tempo(40), result.Score.Tempo);
            Assert.Equal(27, NotesOf(result).Count());
            Assert.Equal([Staff.Treble, Staff.Bass], NotesOf(result).Select(note => note.Staff).Distinct().Order());
            Assert.Equal(2, NotesOf(result).Count(note => note.Slur is not null));
            var dynamics = Assert.Single(Assert.Single(result.Score.Measures).Directions!);
            Assert.Equal((ScoreDirectionKind.Dynamics, "ff", true), (dynamics.Kind, dynamics.Text, dynamics.IsBelow));
            Assert.Equal(1, WarningCount(result, "sound playback hints"));
        }),

        // The guitar chord diagrams are reported; the chord names are kept.
        ["tutorial-chord-symbols.musicxml"] = Imports(result =>
        {
            Assert.Equal(3, result.Score.Measures.Count);
            Assert.Equal(2, result.Score.KeyFifths);
            Assert.Equal(
                ["G6/D", "A(9)", "A11"],
                result.Score.Measures.SelectMany(measure => measure.Directions ?? []).Select(mark => mark.Text));
            Assert.Equal(3, WarningCount(result, "frame"));
        }),

        ["tutorial-hello-world.musicxml"] = Imports(result =>
        {
            var note = Assert.Single(NotesOf(result));
            Assert.Equal(new Pitch(NoteLetter.C, 0, 4), note.Pitch);
            Assert.Equal(new NoteValue(1), note.NoteValue);
            Assert.Empty(result.Warnings);
        }),

        // The first part is unpitched drum notation.
        ["tutorial-percussion.musicxml"] = Fails<NotSupportedException>("<unpitched>"),

        // The first part is a guitar that sounds an octave below its written pitch (<transpose>).
        ["tutorial-tablature.musicxml"] = Fails<NotSupportedException>("<transpose>"),

        // Four measures of a two-staff piece in 2/2 with up to 13 voices; one measure of the sample backs up twice in a
        // row, which is read as starting its last note on the first beat.
        ["voice-direction-element.musicxml"] = Imports(result =>
        {
            Assert.Equal(4, result.Score.Measures.Count);
            Assert.Equal(4, result.Score.KeyFifths);
            Assert.Equal(new TimeSignature(2, new NoteValue(2)), result.Score.TimeSignature);
            Assert.Equal(56, NotesOf(result).Count());
            Assert.Equal(5, RestsOf(result).Count());
            Assert.Equal(1, RestsOf(result).Count(rest => rest.IsMeasureRest));
            Assert.Equal(1, WarningCount(result, "part-symbol"));
            Assert.Equal(2, WarningCount(result, "voice-directions"));
            Assert.Equal(1, WarningCount(result, "backup before the measure start"));
        }),
    };

    public static TheoryData<string> Files => new(Outcomes.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Files))]
    public void Read_W3cSample_ProducesItsPinnedOutcome(string file)
    {
        var outcome = Outcomes[file];
        string path = Path.Combine(FixtureFolder, file);

        if (outcome.ExceptionType is { } expectedType)
        {
            var exception = Assert.ThrowsAny<Exception>(() => new MusicXmlScoreReader().ReadWithWarnings(path));
            Assert.IsType(expectedType, exception);
            Assert.Contains(outcome.MessageFragment!, exception.Message);
            return;
        }

        outcome.Verify!(new MusicXmlScoreReader().ReadWithWarnings(path));
    }

    [Fact]
    public void Samples_FolderAndRows_AreTheSameEighteenFiles()
    {
        string[] onDisk = Directory.GetFiles(FixtureFolder, "*.musicxml")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(18, onDisk.Length);
        Assert.Equal(onDisk, Outcomes.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Samples_ReadmeRecordsProvenanceAndEachFilesHash_SoACopyCannotBeEditedUnnoticed()
    {
        string readme = File.ReadAllText(Path.Combine(FixtureFolder, "README.md"));

        Assert.Matches(new Regex("upstream commit[^`]*`[0-9a-f]{40}`", RegexOptions.IgnoreCase), readme);
        Assert.Contains("https://www.w3.org/community/about/agreements/final/", readme, StringComparison.Ordinal);
        foreach (string file in Outcomes.Keys)
        {
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(FixtureFolder, file))));
            Assert.Contains($"`{file}`", readme, StringComparison.Ordinal);
            Assert.Contains($"`{hash}`", readme, StringComparison.Ordinal);
        }
    }
}
