namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreCommandLineTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Load_ScoreFlagWithoutPath_ReturnsNullAndWritesReadableError()
    {
        using var error = new StringWriter();

        var score = ScoreCommandLine.Load(["--score"], error);

        Assert.Null(score);
        Assert.Contains("--score requires a path", error.ToString());
    }

    [Fact]
    public void Load_ValidScore_ReturnsScoreWithoutError()
    {
        using var error = new StringWriter();

        var score = ScoreCommandLine.Load(["--score", Fixture("single-staff.musicxml")], error);

        Assert.NotNull(score);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("malformed.musicxml", "Could not parse MusicXML")]
    [InlineData("unsupported-grace.musicxml", "<grace>")]
    public void Load_InvalidScore_ReturnsNullAndWritesReaderError(string fixture, string expectedMessage)
    {
        using var error = new StringWriter();

        var score = ScoreCommandLine.Load(["--score", Fixture(fixture)], error);

        Assert.Null(score);
        Assert.Contains(expectedMessage, error.ToString());
    }

    [Fact]
    public void Load_NonexistentScore_ReturnsNullAndWritesReadableError()
    {
        using var error = new StringWriter();
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.musicxml");

        var score = ScoreCommandLine.Load(["--score", path], error);

        Assert.Null(score);
        Assert.Contains("Could not load score", error.ToString());
        Assert.Contains(Path.GetFileName(path), error.ToString());
    }
}
