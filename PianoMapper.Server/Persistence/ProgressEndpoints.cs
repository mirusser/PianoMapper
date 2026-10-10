using System.Text.Json;
using PianoMapper.Practice;

namespace PianoMapper.Server.Persistence;

internal static class ProgressEndpoints
{
    internal static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ProgressApiRoutes.Sessions, ListAsync);
        endpoints.MapPost(ProgressApiRoutes.Sessions, SaveAsync);
        endpoints.MapDelete(ProgressApiRoutes.Sessions, DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ProgressSessionRepository repository,
        int limit = ProgressApiRoutes.MaximumLimit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > ProgressApiRoutes.MaximumLimit)
        {
            return PersistenceProblems.InvalidRequest(
                $"The limit must be between 1 and {ProgressApiRoutes.MaximumLimit}.");
        }

        IReadOnlyList<SightReadingSessionSummary> sessions = await repository
            .ListNewestAsync(LearnerScope.Resolve(), limit, cancellationToken)
            .ConfigureAwait(false);
        return Results.Content(SightReadingSessionSerializer.SerializeAll(sessions), "application/json");
    }

    /// <summary>
    /// Stores the sessions that are not stored yet; sending a session twice is harmless, which is what makes this both
    /// the write for one finished session and the upload that catches the server up. The body is checked entry by
    /// entry through the Core serializer, and one entry it does not accept rejects the whole request.
    /// </summary>
    private static async Task<IResult> SaveAsync(
        JsonElement body,
        ProgressSessionRepository repository,
        CancellationToken cancellationToken)
    {
        if (body.ValueKind != JsonValueKind.Array)
        {
            return PersistenceProblems.InvalidRequest("The body must be a JSON array of sessions.");
        }

        var sessions = new List<SightReadingSessionSummary>();
        foreach (JsonElement element in body.EnumerateArray())
        {
            if (SightReadingSessionSerializer.TryParse(element) is not { } session)
            {
                return PersistenceProblems.InvalidRequest($"Entry {sessions.Count} is not a session this server understands.");
            }

            sessions.Add(session);
        }

        await repository.SaveAsync(LearnerScope.Resolve(), sessions, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        ProgressSessionRepository repository,
        CancellationToken cancellationToken)
    {
        await repository.DeleteAllAsync(LearnerScope.Resolve(), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
