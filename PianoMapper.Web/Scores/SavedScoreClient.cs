using System.Net.Http.Json;
using PianoMapper.Music;
using PianoMapper.Scores;

namespace PianoMapper.Web.Scores;

internal sealed class SavedScoreClient(HttpClient httpClient)
{
    internal async Task<IReadOnlyList<SavedScoreSummary>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<SavedScoreSummary[]>(
            SavedScoreApiRoutes.Collection,
            cancellationToken) ?? [];

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

    private static async Task<SavedScoreDetails> ReadDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SavedScoreDetails>(cancellationToken) ??
            throw new InvalidDataException("The saved-score response was empty.");
    }
}
