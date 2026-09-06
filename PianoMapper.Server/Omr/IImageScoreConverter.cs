namespace PianoMapper.Server.Omr;

internal interface IImageScoreConverter
{
    Task<byte[]> ConvertAsync(
        Stream image,
        string sourceName,
        CancellationToken cancellationToken);
}
