using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// How one note, on one staff, went during an exercise. Unlike <see cref="PitchAttemptSummary"/> it keeps the staff
/// (middle C on the bass staff is a different reading skill from middle C on the treble staff) and how fast the
/// learner found the note. Response time is only recorded for self-paced prompts, where the learner rather than the
/// clock sets the pace.
/// </summary>
/// <param name="AttemptCount">How many prompts asked for this note.</param>
/// <param name="PitchFirstTryCorrectCount">How many of them were answered with the right key first.</param>
/// <param name="ResponseSampleCount">How many of them have a response time.</param>
/// <param name="TotalResponseMilliseconds">The response times added up, or null when there are none.</param>
public sealed record NoteAttemptSummary(
    Pitch Pitch,
    Staff Staff,
    int AttemptCount,
    int PitchFirstTryCorrectCount,
    int ResponseSampleCount,
    long? TotalResponseMilliseconds);
