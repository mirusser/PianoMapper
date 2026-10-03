using PianoMapper.Music;
using PianoMapper.Web.Importing;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreImportStatusTests
{
    private static MusicXmlReadResult ResultWith(params MusicXmlImportWarning[] warnings) =>
        new(
            new Score(
                "lesson",
                new TimeSignature(4, new NoteValue(4)),
                new Tempo(120),
                0,
                [new ScoreMeasure([], []), new ScoreMeasure([], [])]),
            warnings);

    [Fact]
    public void Describe_NoWarnings_ReportsTheLoadedScoreOnly()
    {
        Assert.Equal("Loaded lesson: 2 measure(s).", ScoreImportStatus.Describe(ResultWith()));
    }

    [Fact]
    public void Describe_Warnings_ListsEachConstructWithItsCountInOrder()
    {
        string status = ScoreImportStatus.Describe(ResultWith(
            new MusicXmlImportWarning("lyric", 49),
            new MusicXmlImportWarning("hidden rest", 1),
            new MusicXmlImportWarning("jump instruction", 4)));

        Assert.Equal(
            "Loaded lesson: 2 measure(s). Simplified on import: lyric ×49, hidden rest, jump instruction ×4.",
            status);
    }
}
