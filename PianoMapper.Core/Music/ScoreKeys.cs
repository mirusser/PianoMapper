namespace PianoMapper.Music;

public static class ScoreKeys
{
    /// <summary>
    /// The key signature in effect in every measure: <see cref="Score.KeyFifths"/> until a measure's own
    /// <see cref="ScoreMeasure.KeyFifths"/> changes it, then that key until the next change.
    /// </summary>
    public static int[] GetKeyFifthsByMeasure(Score score)
    {
        ArgumentNullException.ThrowIfNull(score);

        var keys = new int[score.Measures.Count];
        int current = score.KeyFifths;
        for (int index = 0; index < keys.Length; index++)
        {
            current = score.Measures[index].KeyFifths ?? current;
            keys[index] = current;
        }

        return keys;
    }
}
