using PianoMapper.Scores;
using PianoMapper.Server.Assets;
using PianoMapper.Server.Omr;
using PianoMapper.Server.Persistence;

const long maximumImageSizeBytes = 10L * 1024 * 1024;
const string sourceNameHeader = "X-PianoMapper-Source-Name";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumImageSizeBytes);
string databaseConnectionString = builder.Configuration.GetConnectionString(PostgresDatabase.ConnectionStringName) ??
    throw new InvalidOperationException(
        $"ConnectionStrings:{PostgresDatabase.ConnectionStringName} must be configured.");
string audiverisExecutable = builder.Configuration["Omr:AudiverisExecutable"] ?? "audiveris";
int timeoutSeconds = builder.Configuration.GetValue("Omr:TimeoutSeconds", 180);
if (timeoutSeconds <= 0)
{
    throw new InvalidOperationException("Omr:TimeoutSeconds must be greater than zero.");
}

// The alternate recognizer is optional and off until an executable is configured. A run takes minutes, hence the long default.
string? oemerExecutable = builder.Configuration["Omr:OemerExecutable"];
int oemerTimeoutSeconds = builder.Configuration.GetValue("Omr:OemerTimeoutSeconds", 600);
if (oemerTimeoutSeconds <= 0)
{
    throw new InvalidOperationException("Omr:OemerTimeoutSeconds must be greater than zero.");
}

builder.Services.AddSingleton(new AudiverisOptions(
    audiverisExecutable,
    TimeSpan.FromSeconds(timeoutSeconds)));
builder.Services.AddSingleton(new OemerOptions(
    oemerExecutable,
    TimeSpan.FromSeconds(oemerTimeoutSeconds)));
builder.Services.AddSingleton<IAudiverisProcessRunner, AudiverisProcessRunner>();
builder.Services.AddSingleton<IImageScoreConverter, AudiverisImageScoreConverter>();
builder.Services.AddSingleton<AudiverisMusicXmlNormalizer>();
builder.Services.AddSingleton<OemerImageScoreConverter>();
builder.Services.AddSingleton<OemerMusicXmlPostProcessor>();
builder.Services.AddSingleton(services => new PostgresDatabase(
    databaseConnectionString,
    services.GetRequiredService<ILogger<PostgresDatabase>>()));
builder.Services.AddSingleton<SavedScoreRepository>();
builder.Services.AddSingleton<ProgressSessionRepository>();
builder.Services.Configure<StaticFileOptions>(options =>
    options.OnPrepareResponse = context => StaticAssetCachePolicy.Apply(context.Context));

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapPost("/api/score-images/convert", async (
    HttpRequest request,
    IImageScoreConverter converter,
    AudiverisMusicXmlNormalizer normalizer,
    CancellationToken cancellationToken) =>
{
    if (request.ContentLength is > maximumImageSizeBytes)
    {
        return Results.Text("The score image exceeds the 10 MiB limit.", statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    string? sourceName = request.Headers[sourceNameHeader].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(sourceName))
    {
        return Results.Text("The score image file name is missing.", statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        byte[] audiverisMusicXml = await converter.ConvertAsync(request.Body, sourceName, cancellationToken);
        byte[] musicXml = normalizer.Normalize(audiverisMusicXml);
        string downloadName = $"{Path.GetFileNameWithoutExtension(sourceName)}.mxl";
        return Results.File(
            musicXml,
            "application/vnd.recordare.musicxml",
            downloadName);
    }
    catch (AudiverisUnavailableException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (InvalidDataException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (NotSupportedException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
});

// Opt-in second attempt after the standard conversion failed: slower and less precise, so it is a route of its own
// that the learner asks for, never a fallback behind the route above.
app.MapPost(ScoreImageApi.AlternateConversionRoute, async (
    HttpRequest request,
    HttpResponse response,
    OemerImageScoreConverter converter,
    OemerMusicXmlPostProcessor postProcessor,
    CancellationToken cancellationToken) =>
{
    if (request.ContentLength is > maximumImageSizeBytes)
    {
        return Results.Text("The score image exceeds the 10 MiB limit.", statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    string? sourceName = request.Headers[sourceNameHeader].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(sourceName))
    {
        return Results.Text("The score image file name is missing.", statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        byte[] oemerMusicXml = await converter.ConvertAsync(request.Body, sourceName, cancellationToken);
        OemerPostProcessResult recognized = postProcessor.Process(oemerMusicXml);
        if (recognized.SuspectMeasures.Count > 0)
        {
            response.Headers[ScoreImageApi.ImportWarningsHeader] = DescribeSuspectMeasures(recognized.SuspectMeasures);
        }

        string downloadName = $"{Path.GetFileNameWithoutExtension(sourceName)}.mxl";
        return Results.File(
            recognized.CompressedMusicXml,
            "application/vnd.recordare.musicxml",
            downloadName);
    }
    catch (OemerUnavailableException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (InvalidDataException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (NotSupportedException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
});

// The database is prepared on first use, not at startup, so every route that touches it answers 503 while it is down.
var persistence = app.MapGroup(string.Empty).AddEndpointFilter<PersistenceErrorFilter>();
persistence.MapSavedScoreEndpoints();
persistence.MapProgressEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

// The wording the learner sees (the client lists it under "Simplified on import"); ASCII only, as it travels in a header.
static string DescribeSuspectMeasures(IReadOnlyList<int> measures) =>
    $"{(measures.Count == 1 ? "measure" : "measures")} {string.Join(", ", measures)} may be misread";
