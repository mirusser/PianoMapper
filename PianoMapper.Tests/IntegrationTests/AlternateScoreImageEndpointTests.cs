using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PianoMapper.Scores;
using PianoMapper.Server.Omr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PianoMapper.Tests.IntegrationTests;

/// <summary>
/// The alternate recognizer's route on the real server, in process. It needs no database (the connection string
/// points nowhere and the route never touches it) and no oemer: a fake process runner stands in for the program.
/// </summary>
public sealed class AlternateScoreImageEndpointTests : IAsyncLifetime
{
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=pianomapper;Username=pianomapper;Password=unused;Timeout=1";

    private const string SourceNameHeader = "X-PianoMapper-Source-Name";
    private const string MusicXmlMediaType = "application/vnd.recordare.musicxml";
    private const string OemerExecutableSetting = "Omr:OemerExecutable";

    private readonly PianoMapperServerFactory factory = new(UnreachableDatabase);
    private readonly List<HttpClient> clients = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task ConvertAlternate_RecognizedScore_ReturnsAnImportableMxlAndTheMeasuresToCheck()
    {
        var client = CreateClient(configured: true, new FakeProcessRunner(command =>
        {
            string outputDirectory = command.Arguments[command.Arguments.ToList().IndexOf("-o") + 1];
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "oemer-passacaglia-excerpt.musicxml"),
                Path.Combine(outputDirectory, "score.musicxml"));
            return new AudiverisProcessResult(0, string.Empty);
        }));

        using var response = await client.SendAsync(await CreateImageRequestAsync("photo.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MusicXmlMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("photo.mxl", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(
            "measures 1, 3 may be misread",
            Assert.Single(response.Headers.GetValues(ScoreImageApi.ImportWarningsHeader)));
        await using var body = await response.Content.ReadAsStreamAsync();
        Assert.Equal(3, new Music.MusicXmlScoreReader().Read(body, "photo.mxl").Measures.Count);
    }

    [Fact]
    public async Task ConvertAlternate_NoMeasuresToCheck_SendsNoWarningsHeader()
    {
        var client = CreateClient(configured: true, new FakeProcessRunner(command =>
        {
            string outputDirectory = command.Arguments[command.Arguments.ToList().IndexOf("-o") + 1];
            File.WriteAllText(Path.Combine(outputDirectory, "score.musicxml"), """
                <score-partwise>
                  <part-list><score-part id="P1"><part-name>Piano</part-name></score-part></part-list>
                  <part id="P1">
                    <measure number="1">
                      <attributes><divisions>1</divisions></attributes>
                      <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>
                    </measure>
                  </part>
                </score-partwise>
                """);
            return new AudiverisProcessResult(0, string.Empty);
        }));

        using var response = await client.SendAsync(await CreateImageRequestAsync("photo.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(ScoreImageApi.ImportWarningsHeader));
    }

    [Fact]
    public async Task ConvertAlternate_NotConfigured_AnswersUnavailableAndNamesTheSetting()
    {
        var client = CreateClient(configured: false, new FakeProcessRunner(_ =>
            throw new InvalidOperationException("No process should start when oemer is not configured.")));

        using var response = await client.SendAsync(await CreateImageRequestAsync("photo.png"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(OemerExecutableSetting, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ConvertAlternate_MissingSourceName_AnswersBadRequest()
    {
        var client = CreateClient(configured: true, new FakeProcessRunner(_ => new AudiverisProcessResult(0, string.Empty)));
        using var request = await CreateImageRequestAsync("photo.png");
        request.Headers.Remove(SourceNameHeader);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<Exception, HttpStatusCode> RunnerFailures => new()
    {
        { new AudiverisUnavailableException("cannot start", new IOException()), HttpStatusCode.ServiceUnavailable },
        { new TimeoutException("too slow"), HttpStatusCode.GatewayTimeout },
        { new InvalidDataException("unreadable"), HttpStatusCode.UnprocessableEntity },
    };

    [Theory]
    [MemberData(nameof(RunnerFailures))]
    public async Task ConvertAlternate_RunFails_AnswersWithTheMatchingStatus(Exception failure, HttpStatusCode expected)
    {
        var client = CreateClient(configured: true, new FakeProcessRunner(_ => throw failure));

        using var response = await client.SendAsync(await CreateImageRequestAsync("photo.png"));

        Assert.Equal(expected, response.StatusCode);
    }

    private HttpClient CreateClient(bool configured, IAudiverisProcessRunner runner)
    {
        var client = factory
            .WithWebHostBuilder(builder =>
            {
                if (configured)
                {
                    builder.UseSetting(OemerExecutableSetting, "fake-oemer");
                }

                builder.ConfigureTestServices(services => services.AddSingleton(runner));
            })
            .CreateClient();
        clients.Add(client);
        return client;
    }

    private static async Task<HttpRequestMessage> CreateImageRequestAsync(string sourceName)
    {
        using var image = new Image<Rgba32>(10, 10, new Rgba32(255, 255, 255));
        using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        var request = new HttpRequestMessage(HttpMethod.Post, "/" + ScoreImageApi.AlternateConversionRoute)
        {
            Content = new ByteArrayContent(stream.ToArray()),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        request.Headers.Add(SourceNameHeader, sourceName);
        return request;
    }

    private sealed class FakeProcessRunner(
        Func<AudiverisCommand, AudiverisProcessResult> resultFactory) : IAudiverisProcessRunner
    {
        public Task<AudiverisProcessResult> RunAsync(
            AudiverisCommand command,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            Task.FromResult(resultFactory(command));
    }
}
