using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using PianoMapper.Server.Persistence;

namespace PianoMapper.Tests.IntegrationTests;

/// <summary>
/// The real server (<c>Program.cs</c>) in-process, pointed at the given PostgreSQL connection string. The server
/// and the Web project both define a top-level <c>Program</c>, so the server is named by another of its types: the
/// factory only uses the assembly the type lives in.
/// </summary>
internal sealed class PianoMapperServerFactory(string connectionString) : WebApplicationFactory<PostgresDatabase>
{
    internal CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting($"ConnectionStrings:{PostgresDatabase.ConnectionStringName}", connectionString);
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(Logs);
        });
    }
}
