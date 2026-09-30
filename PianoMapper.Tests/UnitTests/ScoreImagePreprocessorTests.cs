using PianoMapper.Server.Omr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreImagePreprocessorTests
{
    [Fact]
    public async Task PrepareAsync_FramedPng_WhitensOuterEdgeAndPreservesInterior()
    {
        var black = new Rgba32(0, 0, 0);
        var white = new Rgba32(255, 255, 255);
        await using var source = new MemoryStream();
        using (var image = new Image<Rgba32>(10, 10, white))
        {
            for (int index = 0; index < image.Width; index++)
            {
                image[index, 0] = black;
                image[index, image.Height - 1] = black;
                image[0, index] = black;
                image[image.Width - 1, index] = black;
            }

            image[5, 5] = black;
            await image.SaveAsPngAsync(source);
        }

        source.Position = 0;
        string outputPath = Path.Combine(Path.GetTempPath(), $"pianomapper-test-{Guid.NewGuid():N}.png");
        try
        {
            await ScoreImagePreprocessor.PrepareAsync(source, outputPath, CancellationToken.None);

            using var result = await Image.LoadAsync<Rgba32>(outputPath);
            for (int index = 0; index < result.Width; index++)
            {
                Assert.Equal(white, result[index, 0]);
                Assert.Equal(white, result[index, result.Height - 1]);
                Assert.Equal(white, result[0, index]);
                Assert.Equal(white, result[result.Width - 1, index]);
            }

            Assert.Equal(black, result[5, 5]);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }
}
