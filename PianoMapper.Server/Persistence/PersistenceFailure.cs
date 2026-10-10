using Npgsql;

namespace PianoMapper.Server.Persistence;

internal static class PersistenceFailure
{
    /// <summary>
    /// PostgreSQL error classes that mean "this database cannot serve us right now" rather than "our statement was
    /// wrong": connection exceptions, invalid authorization, a missing database, insufficient resources and operator
    /// intervention (shutdown, startup).
    /// </summary>
    private static readonly string[] UnavailableSqlStateClasses = ["08", "28", "3D", "53", "57"];

    /// <summary>
    /// Whether the exception means the database could not be reached or used, as opposed to a bug in a statement or in
    /// the data. Those answer 503 and are retried by the caller; everything else stays a server error.
    /// </summary>
    internal static bool IsDatabaseUnavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            PostgresException postgres => postgres.IsTransient ||
                UnavailableSqlStateClasses.Any(sqlStateClass =>
                    postgres.SqlState.StartsWith(sqlStateClass, StringComparison.Ordinal)),
            NpgsqlException => true,
            TimeoutException => true,
            _ => false,
        };
    }
}
