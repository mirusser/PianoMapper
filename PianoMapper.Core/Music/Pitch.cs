using System.Globalization;

namespace PianoMapper.Music;

public readonly record struct Pitch
{
    private static readonly int[] SemitonesFromC = [0, 2, 4, 5, 7, 9, 11];

    public Pitch(NoteLetter letter, int alter, int octave)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(alter, -2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(alter, 2);

        Letter = letter;
        Alter = alter;
        Octave = octave;
    }

    public NoteLetter Letter { get; }

    public int Alter { get; }

    public int Octave { get; }

    public int MidiNumber => ((Octave + 1) * 12) + SemitonesFromC[(int)Letter] + Alter;

    public double Frequency => 440.0 * Math.Pow(2.0, (MidiNumber - 69) / 12.0);

    public int DiatonicIndex => (Octave * 7) + (int)Letter;

    public override string ToString()
    {
        string accidental = Alter switch
        {
            -2 => "bb",
            -1 => "b",
            0 => string.Empty,
            1 => "#",
            2 => "x",
            _ => throw new InvalidOperationException($"Unsupported pitch alteration: {Alter}."),
        };

        return $"{Letter}{accidental}{Octave}";
    }

    public static bool TryParse(string? value, out Pitch pitch)
    {
        pitch = default;
        if (string.IsNullOrEmpty(value) || value.Length < 2 || !TryParseLetter(value[0], out var letter))
        {
            return false;
        }

        int alter = 0;
        int octaveStart = 1;
        if (value.Length > octaveStart)
        {
            switch (value[octaveStart])
            {
                case '#':
                    alter = 1;
                    octaveStart++;
                    break;
                case 'x':
                    alter = 2;
                    octaveStart++;
                    break;
                case 'b':
                    alter = -1;
                    octaveStart++;
                    if (value.Length > octaveStart && value[octaveStart] == 'b')
                    {
                        alter = -2;
                        octaveStart++;
                    }

                    break;
            }
        }

        if (!int.TryParse(value.AsSpan(octaveStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out int octave))
        {
            return false;
        }

        pitch = new Pitch(letter, alter, octave);
        return true;
    }

    private static bool TryParseLetter(char value, out NoteLetter letter)
    {
        letter = char.ToUpperInvariant(value) switch
        {
            'C' => NoteLetter.C,
            'D' => NoteLetter.D,
            'E' => NoteLetter.E,
            'F' => NoteLetter.F,
            'G' => NoteLetter.G,
            'A' => NoteLetter.A,
            'B' => NoteLetter.B,
            _ => (NoteLetter)(-1),
        };

        return (int)letter >= 0;
    }
}
