namespace PianoMapper.Audio;

internal interface IAudioDispatcher : IDisposable
{
    void Enqueue(Action action);

    void StartAudio(PerformedNote note, short[] samples, float gain);

    void StopAudio(PerformedNote note);

    void ClearActiveNotes(IReadOnlyCollection<PerformedNote> activeNotes);

    bool TryGetSamples(PerformedNote note, out short[] samples);

    void RequestSampleOffsetRefresh(PerformedNote note);

    bool TryGetSampleOffset(PerformedNote note, out int sampleOffset);
}
