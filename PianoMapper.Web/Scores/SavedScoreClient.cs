using System.Net.Http.Json;
using PianoMapper.Music;
using PianoMapper.Scores;

namespace PianoMapper.Web.Scores;

internal sealed class SavedScoreClient(HttpClient httpClient)
{
    internal async Task<SavedScorePage> ListAsync(
        int page,
        string? title,
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<SavedScorePage>(
            SavedScoreApiRoutes.List(page, SavedScorePage.DefaultPageSize, title),
            cancellationToken) ?? new SavedScorePage([], 0);

    internal async Task<SavedScoreDetails> FindAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            SavedScoreApiRoutes.ById(id),
            cancellationToken);
        return await ReadDetailsAsync(response, cancellationToken);
    }

    internal async Task<SavedScoreDetails> CreateAsync(
        Score score,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            SavedScoreApiRoutes.Collection,
            score,
            cancellationToken);
        return await ReadDetailsAsync(response, cancellationToken);
    }

    internal async Task<SavedScoreDetails> UpdateAsync(
        Guid id,
        Score score,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            SavedScoreApiRoutes.ById(id),
            score,
            cancellationToken);
        return await ReadDetailsAsync(response, cancellationToken);
    }

    internal async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            SavedScoreApiRoutes.ById(id),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<SavedScoreDetails> ReadDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SavedScoreDetails>(cancellationToken) ??
            throw new InvalidDataException("The saved-score response was empty.");
    }
}
