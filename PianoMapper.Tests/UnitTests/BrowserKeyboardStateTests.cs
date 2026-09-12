using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserKeyboardStateTests
{
    [Theory]
    [InlineData("BracketLeft", (int)BrowserInputCommandKind.PreviousMeasures)]
    [InlineData("BracketRight", (int)BrowserInputCommandKind.NextMeasures)]
    [InlineData("KeyP", (int)BrowserInputCommandKind.StartScorePlayback)]
    [InlineData("KeyT", (int)BrowserInputCommandKind.StartPractice)]
    [InlineData("KeyV", (int)BrowserInputCommandKind.ToggleView)]
    [InlineData("KeyM", (int)BrowserInputCommandKind.PlayRandomMeasure)]
    public void HandleKeyDown_ControlCode_ReturnsCommand(
        string code,
        int expectedKindValue)
    {
        var state = new BrowserKeyboardState(initialOctave: 4);

        var command = state.HandleKeyDown(code, isRepeat: false, TimeSpan.FromSeconds(1));

        Assert.True(command.IsHandled);
        Assert.Equal((BrowserInputCommandKind)expectedKindValue, command.Kind);
    }

    [Fact]
    public void HandleKeyDown_RepeatedControl_ReturnsNoCommand()
    {
        var state = new BrowserKeyboardState(initialOctave: 4);

        var command = state.HandleKeyDown("KeyP", isRepeat: true, TimeSpan.FromSeconds(1));

        Assert.True(command.IsHandled);
        Assert.Equal(BrowserInputCommandKind.None, command.Kind);
    }

    [Theory]
    [InlineData("KeyA")]
    [InlineData("KeyW")]
    [InlineData("Semicolon")]
    [InlineData("KeyZ")]
    [InlineData("KeyX")]
    [InlineData("Digit4")]
    public void HandleKeyDown_RemovedPianoMapping_IsNotHandled(string code)
    {
        var state = new BrowserKeyboardState(initialOctave: 4);

        var command = state.HandleKeyDown(code, isRepeat: false, TimeSpan.FromSeconds(1));

        Assert.False(command.IsHandled);
        Assert.Equal(BrowserInputCommandKind.None, command.Kind);
    }

    [Fact]
    public void ChangeOctave_OutOfRange_ClampsToSupportedNotationRange()
    {
        var state = new BrowserKeyboardState(initialOctave: 4);

        state.ChangeOctave(12, TimeSpan.FromSeconds(1));

        Assert.Equal(8, state.CurrentOctave);
    }

    [Fact]
    public void Clear_ReturnsClearCommand()
    {
        var state = new BrowserKeyboardState(initialOctave: 4);

        var command = state.Clear(TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.Clear, command.Kind);
    }
}
