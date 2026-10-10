namespace PianoMapper.Server.Persistence;

/// <summary>The problem-details answers every persistence route shares, so the wording lives in one place.</summary>
internal static class PersistenceProblems
{
    internal static IResult InvalidRequest(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The request is invalid.",
            detail: detail);

    internal static IResult DatabaseUnavailable() =>
        Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The database is unavailable.",
            detail: "Try again shortly.");
}
