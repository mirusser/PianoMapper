using PianoMapper.Server.Omr;

const long maximumImageSizeBytes = 10L * 1024 * 1024;
const string sourceNameHeader = "X-PianoMapper-Source-Name";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumImageSizeBytes);
string audiverisExecutable = builder.Configuration["Omr:AudiverisExecutable"] ?? "audiveris";
int timeoutSeconds = builder.Configuration.GetValue("Omr:TimeoutSeconds", 180);
if (timeoutSeconds <= 0)
{
    throw new InvalidOperationException("Omr:TimeoutSeconds must be greater than zero.");
}

builder.Services.AddSingleton(new AudiverisOptions(
    audiverisExecutable,
    TimeSpan.FromSeconds(timeoutSeconds)));
builder.Services.AddSingleton<IAudiverisProcessRunner, AudiverisProcessRunner>();
builder.Services.AddSingleton<IImageScoreConverter, AudiverisImageScoreConverter>();
builder.Services.AddSingleton<AudiverisMusicXmlNormalizer>();

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

app.MapFallbackToFile("index.html");

app.Run();
