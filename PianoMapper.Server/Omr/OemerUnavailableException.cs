namespace PianoMapper.Server.Omr;

internal sealed class OemerUnavailableException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
