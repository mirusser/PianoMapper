using System.IO;
using System.Net.Sockets;
using Npgsql;
using PianoMapper.Server.Persistence;

namespace PianoMapper.Tests.UnitTests;

public sealed class PersistenceFailureTests
{
    [Fact]
    public void IsDatabaseUnavailable_ConnectionRefused_IsTrue()
    {
        var exception = new NpgsqlException("Failed to connect", new SocketException((int)SocketError.ConnectionRefused));

        Assert.True(PersistenceFailure.IsDatabaseUnavailable(exception));
    }

    [Fact]
    public void IsDatabaseUnavailable_ConnectionLostMidCommand_IsTrue()
    {
        var exception = new NpgsqlException("Exception while reading from stream", new IOException("broken pipe"));

        Assert.True(PersistenceFailure.IsDatabaseUnavailable(exception));
    }

    [Fact]
    public void IsDatabaseUnavailable_Timeout_IsTrue()
    {
        Assert.True(PersistenceFailure.IsDatabaseUnavailable(new TimeoutException()));
    }

    [Theory]
    [InlineData("08006")]
    [InlineData("28P01")]
    [InlineData("3D000")]
    [InlineData("53300")]
    [InlineData("57P03")]
    public void IsDatabaseUnavailable_ServerSaysItCannotServeUs_IsTrue(string sqlState)
    {
        Assert.True(PersistenceFailure.IsDatabaseUnavailable(CreatePostgresException(sqlState)));
    }

    [Theory]
    [InlineData("23505")]
    [InlineData("42P01")]
    [InlineData("22P02")]
    public void IsDatabaseUnavailable_AnErrorInOurOwnStatement_IsFalse(string sqlState)
    {
        Assert.False(PersistenceFailure.IsDatabaseUnavailable(CreatePostgresException(sqlState)));
    }

    [Fact]
    public void IsDatabaseUnavailable_OtherExceptions_IsFalse()
    {
        Assert.False(PersistenceFailure.IsDatabaseUnavailable(new InvalidOperationException()));
        Assert.False(PersistenceFailure.IsDatabaseUnavailable(new ArgumentException("bad")));
        Assert.False(PersistenceFailure.IsDatabaseUnavailable(new OperationCanceledException()));
    }

    private static PostgresException CreatePostgresException(string sqlState) =>
        new("error", "ERROR", "ERROR", sqlState);
}
