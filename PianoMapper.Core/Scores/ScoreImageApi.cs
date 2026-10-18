namespace PianoMapper.Scores;

/// <summary>
/// The parts of the score-image import contract that the hosted server and the browser client both name: the route of
/// the opt-in alternate recognizer, and the response header through which the server reports measures to check.
/// </summary>
public static class ScoreImageApi
{
    /// <summary>
    /// Relative, like the standard conversion route, so the app still works when it is deployed under a sub-path.
    /// </summary>
    public const string AlternateConversionRoute = "api/score-images/convert-alternate";

    /// <summary>
    /// ASCII-only import warnings for the converted score, joined with <see cref="ImportWarningSeparator"/>.
    /// </summary>
    public const string ImportWarningsHeader = "X-PianoMapper-Import-Warnings";

    public const char ImportWarningSeparator = '|';
}
