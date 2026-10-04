using PianoMapper.Music;
using PianoMapper.Scores;

namespace PianoMapper.Server.Persistence;

internal static class SavedScoreEndpoints
{
    internal static IEndpointRouteBuilder MapSavedScoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(SavedScoreApiRoutes.Collection, ListAsync);
        endpoints.MapGet($"{SavedScoreApiRoutes.Collection}/{{id:guid}}", FindAsync);
        endpoints.MapPost(SavedScoreApiRoutes.Collection, CreateAsync);
        endpoints.MapPut($"{SavedScoreApiRoutes.Collection}/{{id:guid}}", UpdateAsync);
        endpoints.MapDelete($"{SavedScoreApiRoutes.Collection}/{{id:guid}}", DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        SavedScoreRepository repository,
        int page = 1,
        int pageSize = SavedScorePage.DefaultPageSize,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > SavedScorePage.MaximumPageSize)
        {
            return Results.BadRequest("The page and page size are invalid.");
        }

        var scores = await repository.ListAsync(page, pageSize, title, cancellationToken).ConfigureAwait(false);
        return Results.Ok(scores);
    }

    private static async Task<IResult> FindAsync(
        Guid id,
        SavedScoreRepository repository,
        CancellationToken cancellationToken)
    {
        var savedScore = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return savedScore is null ? Results.NotFound() : Results.Ok(savedScore);
    }

    private static async Task<IResult> CreateAsync(
        Score score,
        SavedScoreRepository repository,
        CancellationToken cancellationToken)
    {
        var savedScore = await repository.CreateAsync(score, cancellationToken).ConfigureAwait(false);
        return Results.Created(SavedScoreApiRoutes.ById(savedScore.Id), savedScore);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        Score score,
        SavedScoreRepository repository,
        CancellationToken cancellationToken)
    {
        var savedScore = await repository.UpdateAsync(id, score, cancellationToken).ConfigureAwait(false);
        return savedScore is null ? Results.NotFound() : Results.Ok(savedScore);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        SavedScoreRepository repository,
        CancellationToken cancellationToken)
    {
        bool wasDeleted = await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return wasDeleted ? Results.NoContent() : Results.NotFound();
    }
}
