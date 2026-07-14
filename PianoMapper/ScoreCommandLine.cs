using PianoMapper.Music;

namespace PianoMapper;

internal static class ScoreCommandLine
{
    internal static Score? Load(IReadOnlyList<string> args, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);

        int scoreArgumentIndex = -1;
        for (int index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--score", StringComparison.Ordinal))
            {
                scoreArgumentIndex = index;
                break;
            }
        }

        if (scoreArgumentIndex < 0)
        {
            return null;
        }

        if (scoreArgumentIndex + 1 >= args.Count)
        {
            error.WriteLine("Could not load score: --score requires a path.");
            return null;
        }

        try
        {
            return new MusicXmlScoreReader().Read(args[scoreArgumentIndex + 1]);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            error.WriteLine($"Could not load score: {exception.Message}");
            return null;
        }
    }

}
