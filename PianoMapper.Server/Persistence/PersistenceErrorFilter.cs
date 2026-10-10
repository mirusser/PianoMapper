namespace PianoMapper.Server.Persistence;

/// <summary>
/// Maps what a persistence route can throw onto the HTTP answer: a database that cannot be reached is a 503 and a value
/// the repository refuses is a 400, both as problem details and both logged. The log carries the exception, never the
/// connection string, which Npgsql keeps out of its exception text.
/// </summary>
internal sealed class PersistenceErrorFilter(ILogger<PersistenceErrorFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (PersistenceFailure.IsDatabaseUnavailable(exception))
        {
            logger.LogError(
                exception,
                "The database is unavailable for {Method} {Path}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);
            return PersistenceProblems.DatabaseUnavailable();
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(
                exception,
                "Rejected invalid input for {Method} {Path}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);
            return PersistenceProblems.InvalidRequest(exception.Message);
        }
    }
}
