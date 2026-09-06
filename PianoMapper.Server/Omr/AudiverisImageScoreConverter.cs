using System.Collections.Frozen;

namespace PianoMapper.Server.Omr;

internal sealed class AudiverisImageScoreConverter(
    AudiverisOptions options,
    IAudiverisProcessRunner processRunner) : IImageScoreConverter
{
    private const string FingeringSwitch = "org.audiveris.omr.sheet.ProcessingSwitches.fingerings=true";
    private const string LyricsSwitch = "org.audiveris.omr.sheet.ProcessingSwitches.lyrics=false";
    private const string LyricsAboveStaffSwitch =
        "org.audiveris.omr.sheet.ProcessingSwitches.lyricsAboveStaff=false";

    private static readonly FrozenSet<string> SupportedImageExtensions = new[]
    {
        ".jpeg",
        ".jpg",
        ".png",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public async Task<byte[]> ConvertAsync(
        Stream image,
        string sourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        string extension = Path.GetExtension(sourceName);
        if (!SupportedImageExtensions.Contains(extension))
        {
            throw new NotSupportedException($"Unsupported score image extension '{extension}'.");
        }

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"pianomapper-omr-{Guid.NewGuid():N}");
        string outputDirectory = Path.Combine(temporaryDirectory, "output");
        string inputPath = Path.Combine(temporaryDirectory, $"score{extension.ToLowerInvariant()}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            await using (var inputFile = new FileStream(
                inputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                await image.CopyToAsync(inputFile, cancellationToken);
            }

            string[] arguments =
            [
                "-batch",
                "-transcribe",
                "-export",
                "-constant",
                FingeringSwitch,
                "-constant",
                LyricsSwitch,
                "-constant",
                LyricsAboveStaffSwitch,
                "-output",
                outputDirectory,
                "--",
                inputPath,
            ];
            var result = await processRunner.RunAsync(
                new AudiverisCommand(options.ExecutablePath, arguments),
                options.Timeout,
                cancellationToken);
            if (result.ExitCode != 0)
            {
                string detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"exit code {result.ExitCode}"
                    : result.StandardError.Trim();
                throw new InvalidDataException($"Audiveris could not recognize the score: {detail}");
            }

            string[] outputFiles = Directory
                .EnumerateFiles(outputDirectory, "*.mxl", SearchOption.AllDirectories)
                .ToArray();
            if (outputFiles.Length == 0)
            {
                throw new InvalidDataException("Audiveris did not produce a MusicXML score.");
            }

            if (outputFiles.Length > 1)
            {
                throw new NotSupportedException(
                    "Audiveris produced multiple MusicXML movements; import one movement at a time.");
            }

            return await File.ReadAllBytesAsync(outputFiles[0], cancellationToken);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
