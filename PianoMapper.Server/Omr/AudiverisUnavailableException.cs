namespace PianoMapper.Server.Omr;

internal sealed class AudiverisUnavailableException(
    string message,
    Exception innerException) : Exception(message, innerException);
