namespace PianoMapper.Web.Input;

internal enum BrowserInputCommandKind
{
    None,
    NoteOn,
    NoteOff,
    Clear,
    OctaveChanged,
    PreviousMeasures,
    NextMeasures,
    StartScorePlayback,
    StartPractice,
    ToggleView,
    PlayRandomMeasure,
    ReleaseHeldNotes,
}
