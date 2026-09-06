using System.IO.Compression;
using System.Net;
using System.Text;
using PianoMapper.Web.Importing;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFileImporterTests
{
    [Fact]
    public async Task ReadAsync_MusicXml_ParsesLocallyWithoutCallingOmrEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new InvalidOperationException("The OMR endpoint should not be called for MusicXML."));
        var importer = new ScoreFileImporter(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/"),
        });
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes(CreateScoreXml()));

        var score = await importer.ReadAsync(source, "lesson.musicxml");

        Assert.Equal("lesson", score.Title);
        Assert.Single(Assert.Single(score.Measures).Notes);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ReadAsync_Jpeg_PostsImageAndParsesReturnedMusicXml()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new AsyncOnlyReadStream(CreateCompressedScore())),
        });
        var importer = new ScoreFileImporter(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/"),
        });
        byte[] imageBytes = [0xff, 0xd8, 0xff, 0xd9];
        await using var source = new MemoryStream(imageBytes);

        var score = await importer.ReadAsync(source, "piano-example.jpg");

        Assert.Equal("piano-example", score.Title);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("https://localhost/api/score-images/convert", handler.Request?.RequestUri?.ToString());
        Assert.Equal("image/jpeg", handler.ContentType);
        Assert.Equal("piano-example.jpg", handler.SourceName);
        Assert.Equal(imageBytes, handler.RequestContent);
    }

    [Fact]
    public async Task ReadAsync_OmrFailure_ThrowsServerMessage()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("Audiveris is not installed."),
        });
        var importer = new ScoreFileImporter(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/"),
        });
        await using var source = new MemoryStream([0x89, 0x50, 0x4e, 0x47]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => importer.ReadAsync(source, "score.png"));

        Assert.Equal("Audiveris is not installed.", exception.Message);
    }

    private static string CreateScoreXml() => """
        <score-partwise>
          <part-list><score-part id="P1"><part-name /></score-part></part-list>
          <part id="P1">
            <measure number="1">
              <attributes><divisions>1</divisions></attributes>
              <note>
                <pitch><step>C</step><octave>4</octave></pitch>
                <duration>1</duration><type>quarter</type>
              </note>
            </measure>
          </part>
        </score-partwise>
        """;

    private static byte[] CreateCompressedScore()
    {
        const string containerXml = """
            <container>
              <rootfiles>
                <rootfile full-path="score.xml" media-type="application/vnd.recordare.musicxml+xml" />
              </rootfiles>
            </container>
            """;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "META-INF/container.xml", containerXml);
            WriteEntry(archive, "score.xml", CreateScoreXml());
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(contents);
    }

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        internal HttpRequestMessage? Request { get; private set; }

        internal byte[]? RequestContent { get; private set; }

        internal string? ContentType { get; private set; }

        internal string? SourceName { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            SourceName = request.Headers.TryGetValues("X-PianoMapper-Source-Name", out var values)
                ? Assert.Single(values)
                : null;
            RequestContent = request.Content is null
                ? null
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return responseFactory(request);
        }
    }

    private sealed class AsyncOnlyReadStream(byte[] contents) : Stream
    {
        private readonly MemoryStream inner = new(contents, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("Synchronous reads are not supported.");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
