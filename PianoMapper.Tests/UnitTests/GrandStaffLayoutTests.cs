using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class GrandStaffLayoutTests
{
    [Fact]
    public void GetStaffPosition_MiddleCOnTreble_ReturnsDiatonicLedgerPositionWithoutGeometry()
    {
        var position = GrandStaffLayout.GetStaffPosition(new Pitch(NoteLetter.C, 0, 4), Staff.Treble);

        Assert.Equal(Staff.Treble, position.Staff);
        Assert.Equal(-2, position.DiatonicOffset);
        Assert.Equal([-2], position.LedgerLineOffsets);
    }

    [Fact]
    public void GetPosition_BottomTrebleLine_ReturnsTrebleLineWithoutLedgers()
    {
        var pitch = new Pitch(NoteLetter.E, 0, 4);

        var position = GrandStaffLayout.GetPosition(pitch, Staff.Treble);

        Assert.Equal(GrandStaffLayout.TrebleLineYs[0], position.Y);
        Assert.Empty(position.LedgerLineYs);
    }

    [Theory]
    [InlineData("E4", (int)Staff.Treble, 2, 0)]
    [InlineData("F5", (int)Staff.Treble, 10, 0)]
    [InlineData("A4", (int)Staff.Treble, 5, 0)]
    [InlineData("C4", (int)Staff.Treble, 0, 1)]
    [InlineData("C4", (int)Staff.Bass, 0, 1)]
    [InlineData("G2", (int)Staff.Bass, -10, 0)]
    [InlineData("E2", (int)Staff.Bass, -12, 1)]
    public void GetPosition_AnchorPitch_ReturnsExpectedVerticalPositionAndLedgers(
        string pitchName,
        int staffValue,
        int stepsFromMiddleC,
        int ledgerCount)
    {
        var staff = (Staff)staffValue;
        Assert.True(Pitch.TryParse(pitchName, out var pitch));

        var position = GrandStaffLayout.GetPosition(pitch, staff);

        float expectedY = GrandStaffLayout.MiddleCY + (stepsFromMiddleC * GrandStaffLayout.DiatonicStep);
        Assert.Equal(expectedY, position.Y, 5);
        Assert.Equal(ledgerCount, position.LedgerLineYs.Count);
    }

    [Theory]
    [InlineData("A0", (int)Staff.Bass, 6, -1)]
    [InlineData("C8", (int)Staff.Treble, 9, 1)]
    public void GetPosition_ExtremePitch_ReturnsEveryRequiredLedgerLine(
        string pitchName,
        int staffValue,
        int expectedCount,
        int direction)
    {
        var staff = (Staff)staffValue;
        Assert.True(Pitch.TryParse(pitchName, out var pitch));

        var position = GrandStaffLayout.GetPosition(pitch, staff);

        Assert.Equal(expectedCount, position.LedgerLineYs.Count);
        var staffLines = staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs;
        float edgeLineY = direction > 0 ? staffLines[^1] : staffLines[0];
        for (int index = 0; index < expectedCount; index++)
        {
            float expectedY = edgeLineY + (direction * (index + 1) * 2 * GrandStaffLayout.DiatonicStep);
            Assert.Equal(expectedY, position.LedgerLineYs[index], 5);
        }
    }

    [Theory]
    [InlineData("B3", (int)Staff.Bass, false)]
    [InlineData("C4", (int)Staff.Treble, false)]
    [InlineData("C#4", (int)Staff.Treble, true)]
    public void GetLivePosition_PitchAroundMiddleC_SelectsExpectedStaffAndAccidental(
        string pitchName,
        int expectedStaffValue,
        bool needsAccidental)
    {
        var expectedStaff = (Staff)expectedStaffValue;
        Assert.True(Pitch.TryParse(pitchName, out var pitch));

        var position = GrandStaffLayout.GetLivePosition(pitch);

        Assert.Equal(expectedStaff, position.Staff);
        Assert.Equal(needsAccidental, position.NeedsAccidental);
    }

    [Fact]
    public void MapTimeToX_LiveNote_UsesPianoRollWindowMapping()
    {
        var noteTime = TimeSpan.FromSeconds(4);
        var now = TimeSpan.FromSeconds(10);

        float staffX = GrandStaffLayout.MapTimeToX(noteTime, now);

        float rollX = PianoRollLayout.MapTimeToX(noteTime.TotalSeconds, now.TotalSeconds);
        Assert.Equal(rollX, staffX);
    }

    [Fact]
    public void MapTimeToX_LaterNote_PlacesItToTheRightOfEarlierNote()
    {
        var now = TimeSpan.FromSeconds(10);

        float earlierX = GrandStaffLayout.MapTimeToX(TimeSpan.FromSeconds(3), now);
        float laterX = GrandStaffLayout.MapTimeToX(TimeSpan.FromSeconds(7), now);

        Assert.True(laterX > earlierX);
    }

    [Fact]
    public void GetLiveNoteX_NoteEndedBeforeWindow_ReturnsNull()
    {
        var start = TimeSpan.Zero;
        var end = TimeSpan.FromSeconds(1);
        var now = TimeSpan.FromSeconds(PianoRollLayout.RollingWindowSeconds + 2);

        float? x = GrandStaffLayout.GetLiveNoteX(start, end, now);

        Assert.Null(x);
    }

    [Fact]
    public void MapScoreOnsetToX_MeasureAndBeat_ReturnsProportionalPosition()
    {
        var timeSignature = new TimeSignature(4, new NoteValue(4));

        float firstMeasure = GrandStaffLayout.MapScoreOnsetToX(0, 0, timeSignature, firstVisibleMeasure: 0);
        float nextMeasure = GrandStaffLayout.MapScoreOnsetToX(1, 0, timeSignature, firstVisibleMeasure: 0);
        float halfwayThroughFirst = GrandStaffLayout.MapScoreOnsetToX(0, 2, timeSignature, firstVisibleMeasure: 0);

        Assert.Equal(GrandStaffLayout.ScoreX0, firstMeasure);
        Assert.Equal((firstMeasure + nextMeasure) / 2f, halfwayThroughFirst, 5);
    }

    [Theory]
    [InlineData(1, 0, (int)NoteHeadStyle.Hollow, false, false, 0)]
    [InlineData(2, 0, (int)NoteHeadStyle.Hollow, true, false, 0)]
    [InlineData(4, 0, (int)NoteHeadStyle.Filled, true, false, 0)]
    [InlineData(4, 1, (int)NoteHeadStyle.Filled, true, true, 0)]
    [InlineData(8, 0, (int)NoteHeadStyle.Filled, true, false, 1)]
    [InlineData(16, 0, (int)NoteHeadStyle.Filled, true, false, 2)]
    public void GetScoreNoteLayout_NoteValue_ReturnsHeadDotAndFlagStyle(
        int denominator,
        int dots,
        int expectedHeadValue,
        bool expectedStem,
        bool expectedDot,
        int expectedFlagCount)
    {
        var expectedHead = (NoteHeadStyle)expectedHeadValue;
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(denominator, dots),
            0,
            0,
            Staff.Treble);

        var layout = GrandStaffLayout.GetScoreNoteLayout(note, new TimeSignature(4, new NoteValue(4)), 0);

        Assert.NotNull(layout);
        Assert.Equal(expectedHead, layout.Value.HeadStyle);
        Assert.Equal(expectedStem, layout.Value.HasStem);
        Assert.Equal(expectedDot, layout.Value.HasDot);
        Assert.Equal(expectedFlagCount, layout.Value.FlagCount);
        Assert.Equal(StemDirection.Up, layout.Value.StemDirection);
    }

    [Fact]
    public void GetScoreNoteLayout_PitchAboveMiddleLine_ReturnsDownStemAndAccidental()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.C, 1, 6),
            new NoteValue(4),
            0,
            0,
            Staff.Treble);

        var layout = GrandStaffLayout.GetScoreNoteLayout(note, new TimeSignature(4, new NoteValue(4)), 0);

        Assert.NotNull(layout);
        Assert.Equal(StemDirection.Down, layout.Value.StemDirection);
        Assert.True(layout.Value.Position.NeedsAccidental);
    }

    [Theory]
    [InlineData("D5", ScoreStemDirection.Up, StemDirection.Up)]
    [InlineData("G4", ScoreStemDirection.Down, StemDirection.Down)]
    public void GetScoreNoteLayout_ExplicitStemDirection_OverridesPitchHeuristic(
        string pitchName,
        ScoreStemDirection scoreDirection,
        StemDirection expectedDirection)
    {
        Assert.True(Pitch.TryParse(pitchName, out var pitch));
        var note = new ScoreNote(
            pitch,
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            StemDirection: scoreDirection);

        var layout = GrandStaffLayout.GetScoreNoteLayout(note, new TimeSignature(4, new NoteValue(4)), 0);

        Assert.NotNull(layout);
        Assert.Equal(expectedDirection, layout.Value.StemDirection);
    }

    [Theory]
    [InlineData("G4", (int)Staff.Treble, StemDirection.Up)]
    [InlineData("B4", (int)Staff.Treble, StemDirection.Down)]
    [InlineData("D5", (int)Staff.Treble, StemDirection.Down)]
    [InlineData("B2", (int)Staff.Bass, StemDirection.Up)]
    [InlineData("D3", (int)Staff.Bass, StemDirection.Down)]
    [InlineData("F3", (int)Staff.Bass, StemDirection.Down)]
    public void GetStemDirection_PitchRelativeToMiddleLine_ReturnsUpBelowAndDownAtOrAboveMiddleLine(
        string pitchName,
        int staffValue,
        StemDirection expectedDirection)
    {
        var staff = (Staff)staffValue;
        Assert.True(Pitch.TryParse(pitchName, out var pitch));
        var position = GrandStaffLayout.GetPosition(pitch, staff);

        var stemDirection = GrandStaffLayout.GetStemDirection(position);

        Assert.Equal(expectedDirection, stemDirection);
    }

    [Fact]
    public void GetScoreBarlineXs_SixMeasureWindow_ReturnsMeasureBoundaries()
    {
        var barlines = GrandStaffLayout.GetScoreBarlineXs(firstVisibleMeasure: 0, measureCount: 6);

        Assert.Equal(7, barlines.Count);
        Assert.Equal(GrandStaffLayout.ScoreX0, barlines[0]);
        Assert.Equal(GrandStaffLayout.ScoreX1, barlines[^1]);
    }

    [Fact]
    public void MapAbsoluteBeatToScoreX_PlaybackCursor_MatchesMeasureOnsetMapping()
    {
        var signature = new TimeSignature(4, new NoteValue(4));

        float cursorX = GrandStaffLayout.MapAbsoluteBeatToScoreX(5, signature, firstVisibleMeasure: 0);
        float expectedX = GrandStaffLayout.MapScoreOnsetToX(1, 1, signature, firstVisibleMeasure: 0);

        Assert.Equal(expectedX, cursorX);
    }

    [Fact]
    public void GetLiveFirstVisibleMeasure_CurrentBeatInSecondGroup_ReturnsMeasureGroupStart()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.FromSeconds(13);

        int firstVisibleMeasure = GrandStaffLayout.GetLiveFirstVisibleMeasure(currentTime, signature, tempo);

        Assert.Equal(6, firstVisibleMeasure);
    }

    [Fact]
    public void GetLiveNoteLayout_NoteInCurrentMeasureGroup_MapsOnsetAndDurationToBeatSpace()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.FromSeconds(3);
        var startTime = TimeSpan.FromSeconds(1);

        var layout = GrandStaffLayout.GetLiveNoteLayout(
            new Pitch(NoteLetter.C, 0, 4),
            startTime,
            currentTime,
            currentTime,
            signature,
            tempo);

        Assert.NotNull(layout);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(2, signature, firstVisibleMeasure: 0),
            layout.Value.X);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(6, signature, firstVisibleMeasure: 0),
            layout.Value.DurationEndX);
        Assert.True(layout.Value.DurationEndX > layout.Value.X);
    }

    [Fact]
    public void GetLiveNoteLayout_NoteEndedBeforeCurrentMeasureGroup_ReturnsNull()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.FromSeconds(17);

        var layout = GrandStaffLayout.GetLiveNoteLayout(
            new Pitch(NoteLetter.C, 0, 4),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            currentTime,
            signature,
            tempo);

        Assert.Null(layout);
    }

    [Fact]
    public void GetLiveMeasureGridLines_CurrentBeatInSecondGroup_ReturnsBarlinesMatchingVisibleWindow()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.FromSeconds(9);
        int firstVisibleMeasure = GrandStaffLayout.GetLiveFirstVisibleMeasure(currentTime, signature, tempo);

        var gridLines = GrandStaffLayout.GetLiveMeasureGridLines(currentTime, signature, tempo);

        var expectedBarlineXs = GrandStaffLayout.GetScoreBarlineXs(
            firstVisibleMeasure,
            firstVisibleMeasure + GrandStaffLayout.VisibleMeasureCount);
        var actualBarlineXs = gridLines
            .Where(line => line.Kind == GridLineKind.Barline)
            .Select(line => line.X)
            .ToArray();
        Assert.Equal(expectedBarlineXs, actualBarlineXs);
    }

    [Theory]
    [InlineData(4, 18)]
    [InlineData(3, 12)]
    public void GetLiveMeasureGridLines_TimeSignature_ReturnsBeatTicksExcludingDownbeats(
        int numerator,
        int expectedBeatTickCount)
    {
        var signature = new TimeSignature(numerator, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.Zero;

        var gridLines = GrandStaffLayout.GetLiveMeasureGridLines(currentTime, signature, tempo);

        var beatTicks = gridLines.Where(line => line.Kind == GridLineKind.Beat).ToArray();
        Assert.Equal(expectedBeatTickCount, beatTicks.Length);

        int visibleBeatCount = GrandStaffLayout.VisibleMeasureCount * numerator;
        var expectedXs = Enumerable.Range(1, visibleBeatCount - 1)
            .Where(beatIndex => beatIndex % numerator != 0)
            .Select(beatIndex => GrandStaffLayout.MapAbsoluteBeatToScoreX(beatIndex, signature, firstVisibleMeasure: 0))
            .ToArray();
        Assert.Equal(expectedXs, beatTicks.Select(line => line.X).ToArray());
    }

    [Fact]
    public void GetLiveMeasureGridLines_CursorAtWindowStart_ReturnsCursorLineAtScoreX0()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);

        var gridLines = GrandStaffLayout.GetLiveMeasureGridLines(TimeSpan.Zero, signature, tempo);

        var cursor = Assert.Single(gridLines, line => line.Kind == GridLineKind.Cursor);
        Assert.Equal(GrandStaffLayout.ScoreX0, cursor.X, 5);
    }

    [Fact]
    public void GetLiveMeasureGridLines_CursorBeforeVisibleWindow_OmitsCursorLine()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.FromSeconds(-1);

        var gridLines = GrandStaffLayout.GetLiveMeasureGridLines(currentTime, signature, tempo);

        Assert.DoesNotContain(gridLines, line => line.Kind == GridLineKind.Cursor);
    }

    [Fact]
    public void GetLiveMeasureGridLines_AnyWindow_OrdersBarlinesThenBeatsThenCursor()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);

        var gridLines = GrandStaffLayout.GetLiveMeasureGridLines(TimeSpan.Zero, signature, tempo);

        var kinds = gridLines.Select(line => line.Kind).ToArray();
        int lastBarlineIndex = Array.LastIndexOf(kinds, GridLineKind.Barline);
        int firstBeatIndex = Array.IndexOf(kinds, GridLineKind.Beat);
        int cursorIndex = Array.IndexOf(kinds, GridLineKind.Cursor);
        Assert.True(lastBarlineIndex < firstBeatIndex);
        Assert.True(cursorIndex > Array.LastIndexOf(kinds, GridLineKind.Beat));
    }
}
