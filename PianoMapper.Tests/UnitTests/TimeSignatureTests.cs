using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class TimeSignatureTests
{
    [Fact]
    public void Constructor_NonPositiveNumerator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(0, new NoteValue(4)));
    }
}
