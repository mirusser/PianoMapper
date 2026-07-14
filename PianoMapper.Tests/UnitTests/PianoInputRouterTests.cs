using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoInputRouterTests
{
    [Theory]
    [InlineData(Keys.Escape, false, -1, (int)PianoInputAction.Exit)]
    [InlineData(Keys.Escape, true, (int)PracticeSessionState.Running, (int)PianoInputAction.AbortPractice)]
    [InlineData(Keys.Space, true, (int)PracticeSessionState.CountingIn, (int)PianoInputAction.AbortPractice)]
    [InlineData(Keys.Space, true, (int)PracticeSessionState.Idle, (int)PianoInputAction.Clear)]
    [InlineData(Keys.Enter, true, (int)PracticeSessionState.Finished, (int)PianoInputAction.StartPractice)]
    [InlineData(Keys.Enter, true, (int)PracticeSessionState.Running, (int)PianoInputAction.None)]
    [InlineData(Keys.P, true, (int)PracticeSessionState.Idle, (int)PianoInputAction.StartScorePlayback)]
    [InlineData(Keys.P, true, (int)PracticeSessionState.Running, (int)PianoInputAction.None)]
    [InlineData(Keys.Tab, true, (int)PracticeSessionState.Running, (int)PianoInputAction.ToggleView)]
    [InlineData(Keys.R, true, (int)PracticeSessionState.Running, (int)PianoInputAction.None)]
    public void Resolve_ContextAndKey_ReturnsExpectedAction(
        Keys key,
        bool hasLoadedScore,
        int practiceStateValue,
        int expectedActionValue)
    {
        PracticeSessionState? practiceState = practiceStateValue < 0
            ? null
            : (PracticeSessionState)practiceStateValue;
        var command = PianoInputRouter.Resolve([key], hasLoadedScore, practiceState, octave: 4);

        Assert.Equal((PianoInputAction)expectedActionValue, command.Action);
    }

    [Fact]
    public void Resolve_EscapeAndTabPressed_PrioritizesExit()
    {
        var command = PianoInputRouter.Resolve([Keys.Tab, Keys.Escape], hasLoadedScore: false, practiceState: null, octave: 4);

        Assert.Equal(PianoInputAction.Exit, command.Action);
    }

    [Fact]
    public void Resolve_NumberKey_ReturnsSelectedOctave()
    {
        var command = PianoInputRouter.Resolve([Keys.D7], hasLoadedScore: false, practiceState: null, octave: 2);

        Assert.Equal(PianoInputAction.ChangeOctave, command.Action);
        Assert.Equal(7, command.Octave);
    }
}
