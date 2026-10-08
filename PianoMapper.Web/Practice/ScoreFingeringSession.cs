using PianoMapper.Music;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The fingering state that lasts as long as one loaded score: the notes the learner locked and the alternatives
/// offered by the last regeneration. Locks are addressed in the source score, so a page clears the session whenever
/// that score is replaced and a lock can never reach a different score.
/// </summary>
internal sealed class ScoreFingeringSession
{
    private readonly Dictionary<ScoreFingeringNoteAddress, int> locks = [];

    internal bool HasLocks => locks.Count > 0;

    internal IReadOnlyList<ScoreFingeringAlternative> Alternatives { get; private set; } = [];

    internal int SelectedAlternativeIndex { get; private set; }

    internal bool IsLocked(ScoreFingeringNoteAddress address) => locks.ContainsKey(address);

    /// <summary>Locks the note to <paramref name="fingerNumber"/>, or unlocks it. Returns whether it is now locked.</summary>
    internal bool ToggleLock(ScoreFingeringNoteAddress address, int fingerNumber)
    {
        if (locks.Remove(address))
        {
            return false;
        }

        locks.Add(address, fingerNumber);
        return true;
    }

    /// <summary>Keeps a lock in step with a manual edit: it follows the note's new finger, or goes with a removed one.</summary>
    internal void FollowEdit(ScoreFingeringNoteAddress address, int? fingerNumber)
    {
        if (!locks.ContainsKey(address))
        {
            return;
        }

        if (fingerNumber is int number)
        {
            locks[address] = number;
        }
        else
        {
            locks.Remove(address);
        }
    }

    internal IReadOnlyList<ScoreFingeringLock> CreateLocks() =>
        locks.Select(pair => new ScoreFingeringLock(pair.Key, pair.Value)).ToArray();

    internal void ShowAlternatives(IReadOnlyList<ScoreFingeringAlternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);
        Alternatives = alternatives;
        SelectedAlternativeIndex = 0;
    }

    internal ScoreFingeringAlternative SelectAlternative(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Alternatives.Count);
        SelectedAlternativeIndex = index;
        return Alternatives[index];
    }

    internal void ClearAlternatives()
    {
        Alternatives = [];
        SelectedAlternativeIndex = 0;
    }

    internal void ClearLocks() => locks.Clear();

    internal void Clear()
    {
        ClearLocks();
        ClearAlternatives();
    }
}
