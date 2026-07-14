namespace PianoMapper;

internal readonly record struct PianoInputCommand(PianoInputAction Action, int? Octave = null);
