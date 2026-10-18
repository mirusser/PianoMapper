using System.Collections.Frozen;
using System.Net.Http.Headers;
using PianoMapper.Music;
using PianoMapper.Scores;

namespace PianoMapper.Web.Importing;

internal sealed class ScoreFileImporter(HttpClient httpClient)
{
    private const string ConversionEndpoint = "api/score-images/convert";
    private const string SourceNameHeader = "X-PianoMapper-Source-Name";

    /// <summary>
    /// Added to every alternate-recognizer import: it is slower and less precise than Audiveris, so whatever it read
    /// should be checked against the image even when it reports no measures of its own.
    /// </summary>
    internal const string AlternateRecognizerWarning = "alternate recognizer (less precise)";

    private static readonly FrozenSet<string> MusicXmlExtensions = new[]
    {
        ".mxl",
        ".musicxml",
        ".xml",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> ImageMediaTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpeg"] = "image/jpeg",
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    internal async Task<MusicXmlReadResult> ReadAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        string extension = Path.GetExtension(sourceName);
        if (MusicXmlExtensions.Contains(extension))
        {
            return new MusicXmlScoreReader().ReadWithWarnings(source, sourceName);
        }

        if (!ImageMediaTypes.TryGetValue(extension, out string? mediaType))
        {
            throw new NotSupportedException($"Unsupported score file extension '{extension}'.");
        }

        return await ReadImageAsync(source, mediaType, sourceName, ConversionEndpoint, cancellationToken);
    }

    /// <summary>
    /// Sends a JPEG or PNG to the server's alternate recognizer. The user asks for this after the standard recognizer
    /// failed, so it is never a silent fallback, and the result always carries <see cref="AlternateRecognizerWarning"/>.
    /// </summary>
    internal async Task<MusicXmlReadResult> ReadWithAlternateRecognizerAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        string extension = Path.GetExtension(sourceName);
        if (!ImageMediaTypes.TryGetValue(extension, out string? mediaType))
        {
            throw new NotSupportedException($"The alternate recognizer reads JPEG and PNG images only, not '{extension}'.");
        }

        MusicXmlReadResult result = await ReadImageAsync(
            source,
            mediaType,
            sourceName,
            ScoreImageApi.AlternateConversionRoute,
            cancellationToken);
        return result with
        {
            Warnings = [new MusicXmlImportWarning(AlternateRecognizerWarning, 1), .. result.Warnings],
        };
    }

    internal static bool IsImageFile(string sourceName) =>
        ImageMediaTypes.ContainsKey(Path.GetExtension(sourceName));

    private async Task<MusicXmlReadResult> ReadImageAsync(
        Stream source,
        string mediaType,
        string sourceName,
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StreamContent(source),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        request.Headers.TryAddWithoutValidation(SourceNameHeader, Path.GetFileName(sourceName));

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string message = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            throw new InvalidDataException(string.IsNullOrEmpty(message)
                ? $"Image recognition failed with HTTP status {(int)response.StatusCode}."
                : message);
        }

        byte[] musicXmlBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        using var musicXml = new MemoryStream(musicXmlBytes, writable: false);
        string convertedName = Path.ChangeExtension(sourceName, ".mxl");
        MusicXmlReadResult result = new MusicXmlScoreReader().ReadWithWarnings(musicXml, convertedName);
        return result with { Warnings = [.. ReadServerWarnings(response), .. result.Warnings] };
    }

    private static IEnumerable<MusicXmlImportWarning> ReadServerWarnings(HttpResponseMessage response) =>
        response.Headers.TryGetValues(ScoreImageApi.ImportWarningsHeader, out var values)
            ? values
                .SelectMany(value => value.Split(
                    ScoreImageApi.ImportWarningSeparator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(warning => new MusicXmlImportWarning(warning, 1))
            : [];
}
