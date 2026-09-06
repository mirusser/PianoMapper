using PianoMapper.Server.Omr;

namespace PianoMapper.Tests.UnitTests;

public sealed class AudiverisImageScoreConverterTests
{
    [Fact]
    public async Task ConvertAsync_Jpeg_RunsBatchTranscriptionWithFingeringAndReturnsMxl()
    {
        byte[] expectedMusicXml = [0x50, 0x4b, 0x03, 0x04];
        byte[] imageBytes = [0xff, 0xd8, 0xff, 0xd9];
        var runner = new FakeAudiverisProcessRunner(command =>
        {
            string outputDirectory = GetArgumentAfter(command.Arguments, "-output");
            string inputPath = command.Arguments[^1];
            Assert.Equal(imageBytes, File.ReadAllBytes(inputPath));
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
        Assert.EndsWith(".jpg", runner.Command.Arguments[^1], StringComparison.OrdinalIgnoreCase);
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
        await using var source = new MemoryStream([0x89, 0x50, 0x4e, 0x47]);

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
        await using var source = new MemoryStream([0xff, 0xd8, 0xff, 0xd9]);

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
