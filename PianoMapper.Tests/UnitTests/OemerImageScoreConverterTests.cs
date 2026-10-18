using System.Text;
using PianoMapper.Server.Omr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PianoMapper.Tests.UnitTests;

public sealed class OemerImageScoreConverterTests
{
    [Fact]
    public async Task ConvertAsync_Jpeg_RunsOemerOnAPreparedPngAndReturnsItsMusicXml()
    {
        byte[] expectedMusicXml = Encoding.UTF8.GetBytes("<score-partwise />");
        var runner = new FakeProcessRunner(command =>
        {
            using var preparedImage = Image.Load<Rgba32>(command.Arguments[^1]);
            Assert.Equal(new Rgba32(255, 255, 255), preparedImage[0, 0]);
            string outputDirectory = command.Arguments[Array.IndexOf(command.Arguments.ToArray(), "-o") + 1];
            File.WriteAllBytes(Path.Combine(outputDirectory, "score.musicxml"), expectedMusicXml);
            File.WriteAllBytes(Path.Combine(outputDirectory, "score_teaser.png"), [1, 2, 3]);
            return new AudiverisProcessResult(0, "progress noise");
        });
        var converter = new OemerImageScoreConverter(
            new OemerOptions("custom-oemer", TimeSpan.FromMinutes(10)),
            runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        byte[] result = await converter.ConvertAsync(source, "photo.jpg", CancellationToken.None);

        Assert.Equal(expectedMusicXml, result);
        Assert.Equal("custom-oemer", runner.Command?.ExecutablePath);
        Assert.Contains("-o", runner.Command!.Arguments);
        Assert.EndsWith(".png", runner.Command.Arguments[^1], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromMinutes(10), runner.Timeout);
    }

    [Fact]
    public async Task ConvertAsync_AfterTheRun_RemovesItsTemporaryDirectory()
    {
        string? temporaryDirectory = null;
        var runner = new FakeProcessRunner(command =>
        {
            string outputDirectory = command.Arguments[Array.IndexOf(command.Arguments.ToArray(), "-o") + 1];
            temporaryDirectory = Path.GetDirectoryName(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "score.musicxml"), "<score-partwise />");
            return new AudiverisProcessResult(0, string.Empty);
        });
        var converter = new OemerImageScoreConverter(new OemerOptions("oemer", TimeSpan.FromMinutes(10)), runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: false));

        await converter.ConvertAsync(source, "photo.png", CancellationToken.None);

        Assert.NotNull(temporaryDirectory);
        Assert.False(Directory.Exists(temporaryDirectory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConvertAsync_ExecutableNotConfigured_ThrowsUnavailableWithoutStartingAProcess(string? executable)
    {
        var runner = new FakeProcessRunner(_ => new AudiverisProcessResult(0, string.Empty));
        var converter = new OemerImageScoreConverter(new OemerOptions(executable, TimeSpan.FromMinutes(10)), runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        var exception = await Assert.ThrowsAsync<OemerUnavailableException>(
            () => converter.ConvertAsync(source, "photo.jpg", CancellationToken.None));

        Assert.Contains("Omr:OemerExecutable", exception.Message);
        Assert.Null(runner.Command);
    }

    [Fact]
    public async Task ConvertAsync_ProgramCannotStart_ThrowsUnavailableNamingTheConfiguredPath()
    {
        var runner = new FakeProcessRunner(_ => throw new AudiverisUnavailableException(
            "Could not start Audiveris at '/opt/oemer'.",
            new IOException()));
        var converter = new OemerImageScoreConverter(new OemerOptions("/opt/oemer", TimeSpan.FromMinutes(10)), runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        var exception = await Assert.ThrowsAsync<OemerUnavailableException>(
            () => converter.ConvertAsync(source, "photo.jpg", CancellationToken.None));

        Assert.Contains("/opt/oemer", exception.Message);
        Assert.DoesNotContain("Audiveris", exception.Message);
    }

    [Fact]
    public async Task ConvertAsync_RunExceedsTheTimeout_ThrowsTimeoutNamingTheAlternateRecognizer()
    {
        var runner = new FakeProcessRunner(_ => throw new TimeoutException("Audiveris did not finish within 600 seconds."));
        var converter = new OemerImageScoreConverter(new OemerOptions("oemer", TimeSpan.FromMinutes(10)), runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => converter.ConvertAsync(source, "photo.jpg", CancellationToken.None));

        Assert.Contains("alternate recognizer", exception.Message);
        Assert.DoesNotContain("Audiveris", exception.Message);
    }

    [Fact]
    public async Task ConvertAsync_OemerFails_ReportsOnlyTheLastErrorLine()
    {
        var runner = new FakeProcessRunner(_ => new AudiverisProcessResult(
            1,
            "2026 Extracting stafflines\nTraceback (most recent call last):\n  File \"x.py\", line 1\nIndexError: list index out of range\n"));
        var converter = new OemerImageScoreConverter(new OemerOptions("oemer", TimeSpan.FromMinutes(10)), runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => converter.ConvertAsync(source, "photo.jpg", CancellationToken.None));

        Assert.Contains("IndexError: list index out of range", exception.Message);
        Assert.DoesNotContain("Extracting stafflines", exception.Message);
    }

    [Fact]
    public async Task ConvertAsync_NoMusicXmlOutput_ThrowsReadableError()
    {
        var runner = new FakeProcessRunner(_ => new AudiverisProcessResult(0, string.Empty));
        var converter = new OemerImageScoreConverter(new OemerOptions("oemer", TimeSpan.FromMinutes(10)), runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => converter.ConvertAsync(source, "photo.jpg", CancellationToken.None));

        Assert.Contains("did not produce", exception.Message);
    }

    private static async Task<byte[]> CreateImageBytesAsync(bool saveAsJpeg)
    {
        using var image = new Image<Rgba32>(10, 10, new Rgba32(255, 255, 255));
        for (int index = 0; index < image.Width; index++)
        {
            image[index, 0] = new Rgba32(0, 0, 0);
        }

        await using var stream = new MemoryStream();
        if (saveAsJpeg)
        {
            await image.SaveAsJpegAsync(stream);
        }
        else
        {
            await image.SaveAsPngAsync(stream);
        }

        return stream.ToArray();
    }

    private sealed class FakeProcessRunner(
        Func<AudiverisCommand, AudiverisProcessResult> resultFactory) : IAudiverisProcessRunner
    {
        internal AudiverisCommand? Command { get; private set; }

        internal TimeSpan Timeout { get; private set; }

        public Task<AudiverisProcessResult> RunAsync(
            AudiverisCommand command,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Command = command;
            Timeout = timeout;
            return Task.FromResult(resultFactory(command));
        }
    }
}
