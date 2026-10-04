using PianoMapper.Web.Input;
using PianoMapper.Web.Pages;
using PianoMapper.Web.Rendering;
using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldContinueVisualizationRefresh_GrandStaffState_ReturnsExpectedPolicy(
        bool hasLoadedScore,
        bool hasScheduledKeyboardNotes,
        bool expected)
    {
        var scene = CreateGrandStaffSceneWithVisibleNote();

        bool shouldContinue = Piano.ShouldContinueVisualizationRefresh(
            showPianoRoll: false,
            hasLoadedScore,
            scene,
            activeNoteCount: 0,
            hasScheduledKeyboardNotes: hasScheduledKeyboardNotes);

        Assert.Equal(expected, shouldContinue);
    }

    [Theory]
    [InlineData(20, 0, 1, false, false, true)]
    [InlineData(20, 1, -1, false, false, true)]
    [InlineData(20, 0, -1, false, false, false)]
    [InlineData(20, 3, 1, false, false, false)]
    [InlineData(20, 0, 1, true, false, false)]
    [InlineData(20, 0, 1, false, true, false)]
    public void CanChangeScorePage_Request_ReturnsExpectedPolicy(
        int measureCount,
        int activePageIndex,
        int pageDelta,
        bool isScorePlaybackActive,
        bool isPracticeActive,
        bool expected)
    {
        bool canChange = Piano.CanChangeScorePage(
            measureCount,
            activePageIndex,
            pageDelta,
            isScorePlaybackActive,
            isPracticeActive);

        Assert.Equal(expected, canChange);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    public void CanChangeScorePage_CustomVisibleMeasureCount_UsesConfiguredPageSize(
        int activePageIndex,
        int pageDelta,
        bool expected)
    {
        bool canChange = Piano.CanChangeScorePage(
            measureCount: 6,
            activePageIndex,
            pageDelta,
            isScorePlaybackActive: false,
            isPracticeActive: false,
            visibleMeasureCount: 2);

        Assert.Equal(expected, canChange);
    }

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    public void CanResetScoreHighlights_State_ReturnsExpectedPolicy(
        bool hasScore,
        bool isNoteCheckingEnabled,
        bool isScorePlaybackActive,
        bool isPracticeActive,
        bool expected)
    {
        bool canReset = Piano.CanResetScoreHighlights(
            hasScore,
            isNoteCheckingEnabled,
            isScorePlaybackActive,
            isPracticeActive);

        Assert.Equal(expected, canReset);
    }

    [Fact]
    public void FromPageIndex_RecomputedAfterVisibleMeasureCountChange_KeepsPreviouslyActiveMeasureVisible()
    {
        // Mirrors Piano.SelectVisibleMeasureCountAsync's position-preserving recompute: a user
        // viewing measure 10 (0-indexed) at the old count of 5 changes the count to 3. The new
        // page index is oldFirstMeasure / newVisibleMeasureCount, so the previously active measure
        // stays visible on the newly active row rather than snapping back to page 0.
        const int oldFirstMeasure = 10;
        const int newVisibleMeasureCount = 3;

        var state = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 30,
            oldFirstMeasure / newVisibleMeasureCount,
            newVisibleMeasureCount);

        int activeFirstMeasure = state.ActiveRow == ScoreGrandStaffWindowPair.PhysicalRow.Upper
            ? state.UpperFirstMeasure
            : state.LowerFirstMeasure!.Value;
        Assert.InRange(oldFirstMeasure, activeFirstMeasure, activeFirstMeasure + newVisibleMeasureCount - 1);
    }

    [Theory]
    [InlineData(0, (int)ScoreGrandStaffWindowPair.PhysicalRow.Upper, true)]
    [InlineData(0, (int)ScoreGrandStaffWindowPair.PhysicalRow.Lower, false)]
    [InlineData(1, (int)ScoreGrandStaffWindowPair.PhysicalRow.Upper, false)]
    [InlineData(1, (int)ScoreGrandStaffWindowPair.PhysicalRow.Lower, true)]
    public void GetPerformedNotesForScoreRow_CurrentAndLookAheadRows_ReturnsNotesOnlyForCurrentRow(
        int activePageIndex,
        int renderedRowValue,
        bool expectsNotes)
    {
        var performedNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var windowPair = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 10,
            activePageIndex);

        var result = Piano.GetPerformedNotesForScoreRow(
            [performedNote],
            windowPair,
            (ScoreGrandStaffWindowPair.PhysicalRow)renderedRowValue);

        if (expectsNotes)
        {
            Assert.Same(performedNote, Assert.Single(result));
        }
        else
        {
            Assert.Empty(result);
        }
    }

    [Fact]
    public void GetPerformedNotesForScoreRow_IdleNoteChecking_ReturnsOnlyWrongLiveMarkers()
    {
        var correctNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var wrongNote = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.D, 0, 4),
            StartTime = TimeSpan.Zero,
        };
        var windowPair = ScoreGrandStaffWindowPair.FromPageIndex(
            measureCount: 10,
            activePageIndex: 0);
        var wrongNotes = new HashSet<PerformedNote>(ReferenceEqualityComparer.Instance)
        {
            wrongNote,
        };

        var result = Piano.GetPerformedNotesForScoreRow(
            [correctNote, wrongNote],
            windowPair,
            ScoreGrandStaffWindowPair.PhysicalRow.Upper,
            wrongNotes);

        Assert.Same(wrongNote, Assert.Single(result));
    }

    [Fact]
    public void ScoreOverlaysEqual_DistinctEquivalentHeldNotes_ReturnsTrue()
    {
        var left = new GrandStaffScoreOverlay(
            null,
            [new GrandStaffNote("C4", 0, 0, 0, IsActive: true)],
            []);
        var right = new GrandStaffScoreOverlay(
            null,
            [new GrandStaffNote("C4", 0, 0, 0, IsActive: true)],
            []);

        Assert.True(Piano.ScoreOverlaysEqual(left, right));
    }

    [Fact]
    public void ScoreOverlaysEqual_DifferentHeldNotes_ReturnsFalse()
    {
        var left = new GrandStaffScoreOverlay(
            null,
            [new GrandStaffNote("C4", 0, 0, 0, IsActive: true)],
            []);
        var right = new GrandStaffScoreOverlay(
            null,
            [new GrandStaffNote("D4", 0, 0, 0, IsActive: true)],
            []);

        Assert.False(Piano.ScoreOverlaysEqual(left, right));
    }

    [Fact]
    public void SeparateLiveGrandStaffCursor_CursorLinePresent_ProjectsCursorAndRemovesItFromScene()
    {
        var scene = new GrandStaffScene(
            [
                new GrandStaffLine(-0.92, -0.5, 0.96, -0.5, GrandStaffLineKind.Staff),
                new GrandStaffLine(-0.37, -0.5, -0.37, 0.5, GrandStaffLineKind.Cursor),
            ],
            [],
            []);
        var timeSignature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(60);

        var (cursorFreeScene, cursor) = Piano.SeparateLiveGrandStaffCursor(
            scene,
            TimeSpan.FromSeconds(5),
            timeSignature,
            tempo);

        Assert.DoesNotContain(cursorFreeScene.Lines, line => line.Kind == GrandStaffLineKind.Cursor);
        Assert.Equal(
            new LiveGrandStaffCursorState(60, 4, 0, -0.5, 0.5),
            cursor);
    }

    [Fact]
    public void GetMidiStatusMessage_BrowserWithoutWebMidi_ExplainsWhy()
    {
        var status = new BrowserMidiConnectionStatus { IsSupported = false };

        Assert.NotEmpty(Piano.GetMidiStatusMessage(status));
    }

    [Fact]
    public void GetMidiStatusMessage_PermissionRequired_TellsTheUserWhatToDo()
    {
        var status = new BrowserMidiConnectionStatus { IsSupported = true, IsPermissionRequired = true };

        Assert.NotEmpty(Piano.GetMidiStatusMessage(status));
    }

    [Fact]
    public void GetMidiStatusMessage_NoPianoFound_IsEmptyBecauseTheConnectionSummaryAlreadySaysSo()
    {
        var status = new BrowserMidiConnectionStatus { IsSupported = true };

        Assert.Empty(Piano.GetMidiStatusMessage(status));
    }

    [Theory]
    [InlineData("FP-10 MIDI 1", null)]
    [InlineData(null, "FP-10 MIDI 1")]
    [InlineData("FP-10 MIDI 1", "FP-10 MIDI 1")]
    public void GetMidiStatusMessage_Connected_IsEmptyBecauseTheConnectionSummaryAlreadyShowsTheDevices(
        string? inputName,
        string? outputName)
    {
        var status = new BrowserMidiConnectionStatus
        {
            IsSupported = true,
            InputNames = inputName is null ? [] : [inputName],
            OutputName = outputName,
        };

        Assert.Empty(Piano.GetMidiStatusMessage(status));
    }

    private static GrandStaffScene CreateGrandStaffSceneWithVisibleNote() =>
        new([], [], [new GrandStaffNote("C4", 0, 0, 1, IsActive: false)]);
}
