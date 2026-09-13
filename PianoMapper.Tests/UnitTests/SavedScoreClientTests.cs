using System.Net;
using System.Net.Http.Json;
using PianoMapper.Music;
using PianoMapper.Scores;
using PianoMapper.Web.Scores;

namespace PianoMapper.Tests.UnitTests;

public sealed class SavedScoreClientTests
{
    [Fact]
    public async Task CreateAsync_Score_PostsCollectionAndReadsDetails()
    {
        Score score = CreateScore();
        var expected = new SavedScoreDetails(
            Guid.NewGuid(),
            score,
            DateTimeOffset.Parse("2026-09-12T10:00:00Z"),
            DateTimeOffset.Parse("2026-09-12T10:00:00Z"));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(expected),
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new SavedScoreClient(httpClient);

        SavedScoreDetails actual = await client.CreateAsync(score);

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal(SavedScoreApiRoutes.Collection, handler.RequestUri?.AbsolutePath);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(score.Title, actual.Score.Title);
        Assert.Equal(score.TimeSignature, actual.Score.TimeSignature);
        Assert.Equal(score.Tempo, actual.Score.Tempo);
        Assert.Equal(score.Measures[0].Notes, actual.Score.Measures[0].Notes);
    }

    [Fact]
    public async Task UpdateAsync_SavedScore_PutsItemRoute()
    {
        Guid id = Guid.NewGuid();
        Score score = CreateScore();
        var expected = new SavedScoreDetails(
            id,
            score,
            DateTimeOffset.Parse("2026-09-12T10:00:00Z"),
            DateTimeOffset.Parse("2026-09-12T11:00:00Z"));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected),
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new SavedScoreClient(httpClient);

        SavedScoreDetails actual = await client.UpdateAsync(id, score);

        Assert.Equal(HttpMethod.Put, handler.RequestMethod);
        Assert.Equal(SavedScoreApiRoutes.ById(id), handler.RequestUri?.AbsolutePath);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
    }

    private static Score CreateScore() =>
        new(
            "Saved score",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            1,
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.F, 1, 4), new NoteValue(4), 0, 0, Staff.Treble)],
                    []),
            ]);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        internal HttpMethod? RequestMethod { get; private set; }

        internal Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
