namespace PianoMapper.Server.Omr;

/// <summary>
/// Runs oemer, an alternate, slower and less precise recognizer, as an external process. It returns oemer's MusicXML
/// as written; <see cref="OemerMusicXmlPostProcessor"/> makes that importable.
/// </summary>
internal sealed class OemerImageScoreConverter(
    OemerOptions options,
    IAudiverisProcessRunner processRunner) : IImageScoreConverter
{
    private const string MusicXmlSearchPattern = "*.musicxml";

    public async Task<byte[]> ConvertAsync(
        Stream image,
        string sourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            throw new OemerUnavailableException(
                "The alternate recognizer is not set up. Install oemer with scripts/install-oemer.sh and configure Omr:OemerExecutable.");
        }

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"pianomapper-oemer-{Guid.NewGuid():N}");
        string outputDirectory = Path.Combine(temporaryDirectory, "output");
        string inputPath = Path.Combine(temporaryDirectory, "score.png");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            await ScoreImagePreprocessor.PrepareAsync(image, inputPath, cancellationToken).ConfigureAwait(false);

            AudiverisProcessResult result = await RunOemerAsync(
                new AudiverisCommand(options.ExecutablePath, ["-o", outputDirectory, inputPath]),
                cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new InvalidDataException(
                    $"The alternate recognizer could not recognize the score: {DescribeFailure(result)}");
            }

            string? outputFile = Directory
                .EnumerateFiles(outputDirectory, MusicXmlSearchPattern, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (outputFile is null)
            {
                throw new InvalidDataException("The alternate recognizer did not produce a MusicXML score.");
            }

            return await File.ReadAllBytesAsync(outputFile, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    // The process runner is shared with Audiveris and words its failures for it, so they are restated for oemer here.
    private async Task<AudiverisProcessResult> RunOemerAsync(
        AudiverisCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await processRunner.RunAsync(command, options.Timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (AudiverisUnavailableException exception)
        {
            throw new OemerUnavailableException(
                $"Could not start the alternate recognizer at '{command.ExecutablePath}'. Install oemer with scripts/install-oemer.sh or configure Omr:OemerExecutable.",
                exception);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"The alternate recognizer did not finish within {options.Timeout.TotalSeconds:F0} seconds.",
                exception);
        }
    }

    // oemer logs progress and library warnings to standard error, so only a Python error's own last line says what failed.
    private static string DescribeFailure(AudiverisProcessResult result)
    {
        string? lastLine = result.StandardError
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => line.Length > 0);
        return lastLine ?? $"exit code {result.ExitCode}";
    }
}
