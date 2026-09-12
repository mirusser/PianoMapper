using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserKeyBindingsTests
{
    [Theory]
    [InlineData("KeyC", (int)BrowserKeyActionKind.Clear)]
    [InlineData("BracketLeft", (int)BrowserKeyActionKind.PreviousMeasures)]
    [InlineData("BracketRight", (int)BrowserKeyActionKind.NextMeasures)]
    [InlineData("KeyP", (int)BrowserKeyActionKind.PlayScore)]
    [InlineData("KeyT", (int)BrowserKeyActionKind.StartPractice)]
    [InlineData("KeyV", (int)BrowserKeyActionKind.ToggleView)]
    [InlineData("KeyM", (int)BrowserKeyActionKind.PlayRandomMeasure)]
    public void TryGetAction_ControlCode_ReturnsControl(string code, int expectedKindValue)
    {
        bool found = BrowserKeyBindings.TryGetAction(code, out var action);

        Assert.True(found);
        Assert.Equal((BrowserKeyActionKind)expectedKindValue, action.Kind);
    }

    [Theory]
    [InlineData("Space")]
    [InlineData("Tab")]
    [InlineData("Enter")]
    [InlineData("Escape")]
    [InlineData("PageUp")]
    [InlineData("PageDown")]
    [InlineData("ArrowUp")]
    [InlineData("ArrowDown")]
    [InlineData("ArrowLeft")]
    [InlineData("ArrowRight")]
    [InlineData("KeyA")]
    [InlineData("KeyW")]
    [InlineData("KeyR")]
    [InlineData("Semicolon")]
    [InlineData("KeyZ")]
    [InlineData("KeyX")]
    [InlineData("Digit1")]
    [InlineData("Digit8")]
    public void HandledCodes_UnmappedCode_IsNotRegistered(string code)
    {
        Assert.DoesNotContain(code, BrowserKeyBindings.HandledCodes);
        Assert.False(BrowserKeyBindings.TryGetAction(code, out _));
    }
}
