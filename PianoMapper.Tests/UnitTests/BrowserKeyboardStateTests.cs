using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserKeyboardStateTests
{
    // The full code-to-action table is BrowserKeyBindings's own responsibility and is
    // exhaustively covered by BrowserKeyBindingsTests; these cases just prove HandleKeyDown
    // delegates to it and translates the result into a BrowserInputCommandKind.
    [Theory]
    [InlineData("BracketLeft", (int)BrowserInputCommandKind.PreviousMeasures)]
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

    // See BrowserKeyBindingsTests for the exhaustive unmapped-code table.
    [Theory]
    [InlineData("KeyA")]
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
