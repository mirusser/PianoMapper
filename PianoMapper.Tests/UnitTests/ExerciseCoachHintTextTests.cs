using PianoMapper.Music;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ExerciseCoachHintTextTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch D4 = new(NoteLetter.D, 0, 4);

    [Fact]
    public void Describe_DirectionHintForAnInterval_NamesTheKeyPressedAndHowFarToGo()
    {
        var hint = new ExerciseCoachHint(
            ExerciseCoachHintLevel.Direction,
            [C4],
            D4,
            PitchDistance.Measure(D4, C4));

        string text = ExerciseCoachHintText.Describe(hint);

        Assert.Contains("D4", text);
        Assert.Contains(PitchDistance.Describe(D4, C4), text);
    }

    [Fact]
    public void Describe_DirectionHintForTheRightNoteInTheWrongOctave_SaysSo()
    {
        var pressed = new Pitch(NoteLetter.C, 0, 5);
        var hint = new ExerciseCoachHint(
            ExerciseCoachHintLevel.Direction,
            [C4],
            pressed,
            PitchDistance.Measure(pressed, C4));

        string text = ExerciseCoachHintText.Describe(hint);

        Assert.Contains("octave", text);
        Assert.Contains("lower", text);
    }

    [Fact]
    public void Describe_DirectionHintForTheWrongAccidental_SaysSo()
    {
        var pressed = new Pitch(NoteLetter.C, 1, 4);
        var hint = new ExerciseCoachHint(
            ExerciseCoachHintLevel.Direction,
            [C4],
            pressed,
            PitchDistance.Measure(pressed, C4));

        string text = ExerciseCoachHintText.Describe(hint);

        Assert.Contains("C#4", text);
        Assert.Contains("accidental", text);
    }

    [Fact]
    public void Describe_NameHint_NamesTheNoteOrEveryNoteOfAChord()
    {
        var single = new ExerciseCoachHint(ExerciseCoachHintLevel.Name, [C4], D4, null);
        var chord = new ExerciseCoachHint(
            ExerciseCoachHintLevel.Name,
            [C4, new Pitch(NoteLetter.E, 0, 4), new Pitch(NoteLetter.G, 0, 4)],
            D4,
            null);

        Assert.Contains("C4", ExerciseCoachHintText.Describe(single));
        string chordText = ExerciseCoachHintText.Describe(chord);
        Assert.Contains("C4", chordText);
        Assert.Contains("E4", chordText);
        Assert.Contains("G4", chordText);
    }

    [Fact]
    public void Describe_DirectionHintWithoutADifference_FallsBackToAGenericNudge()
    {
        var hint = new ExerciseCoachHint(ExerciseCoachHintLevel.Direction, [C4], null, null);

        Assert.False(string.IsNullOrWhiteSpace(ExerciseCoachHintText.Describe(hint)));
    }
}
