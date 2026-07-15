using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserKeyBindingsTests
{
    [Theory]
    [InlineData("KeyA", 0)]
    [InlineData("KeyW", 1)]
    [InlineData("KeyR", 6)]
    [InlineData("Semicolon", 12)]
    public void TryGetAction_NoteCode_ReturnsSemitoneOffset(string code, int expectedOffset)
    {
        bool found = BrowserKeyBindings.TryGetAction(code, out var action);

        Assert.True(found);
        Assert.Equal(BrowserKeyActionKind.Note, action.Kind);
        Assert.Equal(expectedOffset, action.Value);
    }

    [Theory]
    [InlineData("KeyC", (int)BrowserKeyActionKind.Clear, 0)]
    [InlineData("KeyZ", (int)BrowserKeyActionKind.OctaveDown, 0)]
    [InlineData("KeyX", (int)BrowserKeyActionKind.OctaveUp, 0)]
    [InlineData("Digit1", (int)BrowserKeyActionKind.SelectOctave, 1)]
    [InlineData("Digit8", (int)BrowserKeyActionKind.SelectOctave, 8)]
    [InlineData("BracketLeft", (int)BrowserKeyActionKind.PreviousMeasures, 0)]
    [InlineData("BracketRight", (int)BrowserKeyActionKind.NextMeasures, 0)]
    [InlineData("KeyP", (int)BrowserKeyActionKind.PlayScore, 0)]
    [InlineData("KeyT", (int)BrowserKeyActionKind.StartPractice, 0)]
    [InlineData("KeyV", (int)BrowserKeyActionKind.ToggleView, 0)]
    [InlineData("KeyM", (int)BrowserKeyActionKind.PlayRandomMeasure, 0)]
    public void TryGetAction_ControlCode_ReturnsControl(string code, int expectedKindValue, int expectedValue)
    {
        bool found = BrowserKeyBindings.TryGetAction(code, out var action);

        Assert.True(found);
        Assert.Equal((BrowserKeyActionKind)expectedKindValue, action.Kind);
        Assert.Equal(expectedValue, action.Value);
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
    public void HandledCodes_ReservedCode_IsNotRegistered(string code)
    {
        Assert.DoesNotContain(code, BrowserKeyBindings.HandledCodes);
        Assert.False(BrowserKeyBindings.TryGetAction(code, out _));
    }
}
