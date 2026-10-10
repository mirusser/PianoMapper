using System.Net;
using System.Text;
using System.Text.Json;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The progress routes of the hosted server over the registered <see cref="HttpClient"/>. It never throws for a
/// server problem, it says how far the call got: a host with no such routes (the standalone static app answers 404,
/// 405, or its own index page) is <see cref="ProgressServerReach.NoServer"/>, and anything else that goes wrong
/// (network error, timeout, 503, a failing response) is <see cref="ProgressServerReach.Unreachable"/>. A caller that
/// cancels still gets the cancellation.
/// </summary>
internal sealed class ProgressServerClient(HttpClient httpClient) : IProgressServer
{
    private const string JsonMediaType = "application/json";

    public async Task<ProgressServerSessions> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                ProgressApiRoutes.List(ProgressApiRoutes.MaximumLimit),
                cancellationToken);
            ProgressServerReach reach = ToReach(response);
            if (reach == ProgressServerReach.Reached && response.Content.Headers.ContentType?.MediaType != JsonMediaType)
            {
                // A static host answering a missing path with its index page.
                reach = ProgressServerReach.NoServer;
            }

            return reach == ProgressServerReach.Reached
                ? new ProgressServerSessions(reach, await ReadSessionsAsync(response, cancellationToken))
                : new ProgressServerSessions(reach, []);
        }
        catch (Exception exception) when (IsServerProblem(exception, cancellationToken))
        {
            return new ProgressServerSessions(ProgressServerReach.Unreachable, []);
        }
    }

    public async Task<ProgressServerReach> UploadAsync(
        IReadOnlyList<SightReadingSessionSummary> sessions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        using var request = new HttpRequestMessage(HttpMethod.Post, ProgressApiRoutes.Sessions)
        {
            Content = new StringContent(SightReadingSessionSerializer.SerializeAll(sessions), Encoding.UTF8, JsonMediaType),
        };
        return await SendAsync(request, cancellationToken);
    }

    public async Task<ProgressServerReach> ClearAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, ProgressApiRoutes.Sessions);
        return await SendAsync(request, cancellationToken);
    }

    private async Task<ProgressServerReach> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            return ToReach(response);
        }
        catch (Exception exception) when (IsServerProblem(exception, cancellationToken))
        {
            return ProgressServerReach.Unreachable;
        }
    }

    private static ProgressServerReach ToReach(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed => ProgressServerReach.NoServer,
        _ when response.IsSuccessStatusCode => ProgressServerReach.Reached,
        _ => ProgressServerReach.Unreachable,
    };

    /// <summary>
    /// A network error, a timeout (the client cancels the request, not the caller) or a body that is not a session
    /// list; a cancellation the caller asked for is not one.
    /// </summary>
    private static bool IsServerProblem(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        HttpRequestException or JsonException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };

    private static async Task<IReadOnlyList<SightReadingSessionSummary>> ReadSessionsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? [.. document.RootElement.EnumerateArray()
                .Select(SightReadingSessionSerializer.TryParse)
                .OfType<SightReadingSessionSummary>()]
            : throw new JsonException("The progress list was not a JSON array.");
    }
}
