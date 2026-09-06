using System.Collections.Frozen;
using System.Net.Http.Headers;
using PianoMapper.Music;

namespace PianoMapper.Web.Importing;

internal sealed class ScoreFileImporter(HttpClient httpClient)
{
    private const string ConversionEndpoint = "api/score-images/convert";
    private const string SourceNameHeader = "X-PianoMapper-Source-Name";

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

    internal async Task<Score> ReadAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        string extension = Path.GetExtension(sourceName);
        if (MusicXmlExtensions.Contains(extension))
        {
            return new MusicXmlScoreReader().Read(source, sourceName);
        }

        if (!ImageMediaTypes.TryGetValue(extension, out string? mediaType))
        {
            throw new NotSupportedException($"Unsupported score file extension '{extension}'.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, ConversionEndpoint)
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
        return new MusicXmlScoreReader().Read(musicXml, convertedName);
    }
}
