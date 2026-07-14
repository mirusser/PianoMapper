using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ConstsTests
{
    [Fact]
    public void GenerateKeyToPitchMapping_OctaveFour_MapsThirteenChromaticKeys()
    {
        IReadOnlyDictionary<Keys, Pitch> mapping = Consts.GenerateKeyToPitchMapping(4);

        (Keys Key, Pitch Pitch, double OldFrequency)[] expected =
        [
            (Keys.A, new Pitch(NoteLetter.C, 0, 4), 261.60),
            (Keys.W, new Pitch(NoteLetter.C, 1, 4), 277.10),
            (Keys.S, new Pitch(NoteLetter.D, 0, 4), 293.63),
            (Keys.E, new Pitch(NoteLetter.D, 1, 4), 311.12),
            (Keys.D, new Pitch(NoteLetter.E, 0, 4), 329.60),
            (Keys.F, new Pitch(NoteLetter.F, 0, 4), 349.19),
            (Keys.R, new Pitch(NoteLetter.F, 1, 4), 369.97),
            (Keys.J, new Pitch(NoteLetter.G, 0, 4), 392.00),
            (Keys.U, new Pitch(NoteLetter.G, 1, 4), 415.30),
            (Keys.K, new Pitch(NoteLetter.A, 0, 4), 440.00),
            (Keys.I, new Pitch(NoteLetter.A, 1, 4), 466.16),
            (Keys.L, new Pitch(NoteLetter.B, 0, 4), 493.87),
            (Keys.Semicolon, new Pitch(NoteLetter.C, 0, 5), 523.20),
        ];

        Assert.Equal(expected.Length, mapping.Count);
        foreach (var item in expected)
        {
            Assert.Equal(item.Pitch, mapping[item.Key]);
            double relativeDifference = Math.Abs(mapping[item.Key].Frequency - item.OldFrequency) / item.OldFrequency;
            Assert.InRange(relativeDifference, 0.0, 0.0005);
        }
    }
}
