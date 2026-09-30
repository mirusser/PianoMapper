using PianoMapper.Server.Omr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PianoMapper.Tests.UnitTests;

public sealed class AudiverisImageScoreConverterTests
{
    [Fact]
    public async Task ConvertAsync_Jpeg_RunsBatchTranscriptionWithFingeringAndReturnsMxl()
    {
        byte[] expectedMusicXml = [0x50, 0x4b, 0x03, 0x04];
        byte[] imageBytes = await CreateImageBytesAsync(saveAsJpeg: true);
        var runner = new FakeAudiverisProcessRunner(command =>
        {
            string outputDirectory = GetArgumentAfter(command.Arguments, "-output");
            string inputPath = command.Arguments[^1];
            using var preparedImage = Image.Load<Rgba32>(inputPath);
            Assert.Equal(new Rgba32(255, 255, 255), preparedImage[0, 0]);
            File.WriteAllBytes(Path.Combine(outputDirectory, "piano-example.mxl"), expectedMusicXml);
            return new AudiverisProcessResult(0, string.Empty);
        });
        var converter = new AudiverisImageScoreConverter(
            new AudiverisOptions("custom-audiveris", TimeSpan.FromMinutes(2)),
            runner);
        await using var source = new MemoryStream(imageBytes);

        byte[] result = await converter.ConvertAsync(source, "piano-example.jpg", CancellationToken.None);

        Assert.Equal(expectedMusicXml, result);
        Assert.NotNull(runner.Command);
        Assert.Equal("custom-audiveris", runner.Command.ExecutablePath);
        Assert.Contains("-batch", runner.Command.Arguments);
        Assert.Contains("-transcribe", runner.Command.Arguments);
        Assert.Contains("-export", runner.Command.Arguments);
        Assert.Contains("org.audiveris.omr.sheet.ProcessingSwitches.fingerings=true", runner.Command.Arguments);
        Assert.Contains("org.audiveris.omr.sheet.ProcessingSwitches.lyrics=false", runner.Command.Arguments);
        Assert.Equal("--", runner.Command.Arguments[^2]);
        Assert.EndsWith(".png", runner.Command.Arguments[^1], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromMinutes(2), runner.Timeout);
    }

    [Fact]
    public async Task ConvertAsync_AudiverisFailure_ThrowsReadableError()
    {
        var runner = new FakeAudiverisProcessRunner(_ =>
            new AudiverisProcessResult(1, "No staff lines found."));
        var converter = new AudiverisImageScoreConverter(
            new AudiverisOptions("audiveris", TimeSpan.FromMinutes(2)),
            runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: false));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => converter.ConvertAsync(source, "score.png", CancellationToken.None));

        Assert.Contains("No staff lines found.", exception.Message);
    }

    [Fact]
    public async Task ConvertAsync_NoMusicXmlOutput_ThrowsReadableError()
    {
        var runner = new FakeAudiverisProcessRunner(_ =>
            new AudiverisProcessResult(0, string.Empty));
        var converter = new AudiverisImageScoreConverter(
            new AudiverisOptions("audiveris", TimeSpan.FromMinutes(2)),
            runner);
        await using var source = new MemoryStream(await CreateImageBytesAsync(saveAsJpeg: true));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => converter.ConvertAsync(source, "score.jpg", CancellationToken.None));

        Assert.Contains("did not produce", exception.Message);
    }

    private static string GetArgumentAfter(IReadOnlyList<string> arguments, string option)
    {
        int index = arguments.ToList().IndexOf(option);
        Assert.InRange(index, 0, arguments.Count - 2);
        return arguments[index + 1];
    }

    private static async Task<byte[]> CreateImageBytesAsync(bool saveAsJpeg)
    {
        var white = new Rgba32(255, 255, 255);
        var black = new Rgba32(0, 0, 0);
        using var image = new Image<Rgba32>(10, 10, white);
        for (int index = 0; index < image.Width; index++)
        {
            image[index, 0] = black;
            image[index, image.Height - 1] = black;
            image[0, index] = black;
            image[image.Width - 1, index] = black;
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

    private sealed class FakeAudiverisProcessRunner(
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
