using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace PianoMapper.Server.Omr;

internal static class ScoreImagePreprocessor
{
    private const int ClearedEdgeWidthPixels = 2;

    // Score uploads are PNG or JPEG only. Registering just those decoders keeps ImageSharp's other
    // decoders (TIFF in particular, see the NuGetAuditSuppress entries in the csproj) unreachable
    // from uploaded content, which is sniffed by magic bytes rather than trusted by file extension.
    private static readonly Configuration DecoderConfiguration = new(
        new PngConfigurationModule(),
        new JpegConfigurationModule());

    internal static async Task PrepareAsync(
        Stream source,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        Image<Rgba32> image;
        try
        {
            image = await Image.LoadAsync<Rgba32>(DecoderConfiguration, source, cancellationToken).ConfigureAwait(false);
        }
        catch (UnknownImageFormatException exception)
        {
            throw new InvalidDataException("The score image format could not be decoded.", exception);
        }
        catch (InvalidImageContentException exception)
        {
            throw new InvalidDataException("The score image contains invalid data.", exception);
        }

        using (image)
        {
            ClearOuterEdge(image);
            await image.SaveAsPngAsync(outputPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ClearOuterEdge(Image<Rgba32> image)
    {
        int edgeWidth = Math.Min(ClearedEdgeWidthPixels, Math.Min(image.Width, image.Height));
        var white = new Rgba32(255, 255, 255);
        for (int offset = 0; offset < edgeWidth; offset++)
        {
            int bottom = image.Height - offset - 1;
            for (int x = 0; x < image.Width; x++)
            {
                image[x, offset] = white;
                image[x, bottom] = white;
            }

            int right = image.Width - offset - 1;
            for (int y = 0; y < image.Height; y++)
            {
                image[offset, y] = white;
                image[right, y] = white;
            }
        }
    }
}
