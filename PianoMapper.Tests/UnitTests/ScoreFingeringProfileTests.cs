using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringProfileTests
{
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(double.NaN, 2)]
    [InlineData(4, 3)]
    [InlineData(4, double.NaN)]
    public void FingeringReach_InvalidSpans_Throw(double comfortable, double maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FingeringReach(comfortable, maximum));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 6)]
    [InlineData(3, 3)]
    public void FingeringFingerPairLimit_InvalidFingers_Throw(int firstFinger, int secondFinger)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FingeringFingerPairLimit(firstFinger, secondFinger, new FingeringReach(1, 2)));
    }

    [Fact]
    public void FingeringHandProfile_SamePairLimitedTwice_Throws()
    {
        var reach = new FingeringReach(1, 2);

        Assert.Throws<ArgumentException>(() => new FingeringHandProfile(
            reach,
            [new FingeringFingerPairLimit(1, 5, reach), new FingeringFingerPairLimit(5, 1, reach)]));
    }

    [Fact]
    public void ScoreFingeringProfile_CreateWithThumbToLittleFingerReach_ClampsEveryOtherPairToTheThumbToLittleFingerReach()
    {
        ScoreFingeringProfile profile = ScoreFingeringProfile.CreateWithThumbToLittleFingerReach(
            new FingeringReach(2, 3),
            new FingeringReach(2, 3));

        foreach (FingeringHandProfile hand in new[] { profile.RightHand, profile.LeftHand })
        {
            for (int lowerFinger = 1; lowerFinger <= 4; lowerFinger++)
            {
                for (int upperFinger = lowerFinger + 1; upperFinger <= 5; upperFinger++)
                {
                    FingeringReach reach = hand.GetReach(lowerFinger, upperFinger);
                    Assert.True(reach.MaximumWhiteKeySpan <= 3, $"{lowerFinger}-{upperFinger} maximum");
                    Assert.True(reach.ComfortableWhiteKeySpan <= 2, $"{lowerFinger}-{upperFinger} comfortable");
                }
            }
        }
    }

    [Fact]
    public void ScoreFingeringGenerationOptions_ConflictingLocksForOneNote_Throws()
    {
        var address = new ScoreFingeringNoteAddress(0, 0);

        Assert.Throws<ArgumentException>(() => new ScoreFingeringGenerationOptions(
            locks: [new ScoreFingeringLock(address, 1), new ScoreFingeringLock(address, 2)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void ScoreFingeringGenerationOptions_AlternativeCountOutsideOneToThree_Throws(int maximumAlternatives)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ScoreFingeringGenerationOptions(maximumAlternatives: maximumAlternatives));
    }

    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 6)]
    public void ScoreFingeringLock_InvalidAddressOrFinger_Throws(int measureIndex, int noteIndex, int finger)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ScoreFingeringLock(new ScoreFingeringNoteAddress(measureIndex, noteIndex), finger));
    }
}
