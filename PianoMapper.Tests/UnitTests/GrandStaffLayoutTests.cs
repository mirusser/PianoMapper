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

    [Theory]
    [InlineData("E4", (int)Staff.Treble, 0)]
    [InlineData("F4", (int)Staff.Treble, 1)]
    [InlineData("G4", (int)Staff.Treble, 2)]
    [InlineData("A4", (int)Staff.Treble, 3)]
    [InlineData("B4", (int)Staff.Treble, 4)]
    [InlineData("C5", (int)Staff.Treble, 5)]
    [InlineData("D5", (int)Staff.Treble, 6)]
    [InlineData("E5", (int)Staff.Treble, 7)]
    [InlineData("F5", (int)Staff.Treble, 8)]
    [InlineData("G2", (int)Staff.Bass, 0)]
    [InlineData("A2", (int)Staff.Bass, 1)]
    [InlineData("B2", (int)Staff.Bass, 2)]
    [InlineData("C3", (int)Staff.Bass, 3)]
    [InlineData("D3", (int)Staff.Bass, 4)]
    [InlineData("E3", (int)Staff.Bass, 5)]
    [InlineData("F3", (int)Staff.Bass, 6)]
    [InlineData("G3", (int)Staff.Bass, 7)]
    [InlineData("A3", (int)Staff.Bass, 8)]
    public void GetStaffPosition_StandardLineOrSpacePitch_ReturnsConventionalBottomToTopOffset(
        string pitchName,
        int staffValue,
        int expectedOffset)
    {
        var staff = (Staff)staffValue;
        Assert.True(Pitch.TryParse(pitchName, out var pitch));

        var position = GrandStaffLayout.GetStaffPosition(pitch, staff);

        Assert.Equal(expectedOffset, position.DiatonicOffset);
        Assert.Empty(position.LedgerLineOffsets);
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

    [Fact]
    public void MapScoreOnsetToX_CustomVisibleMeasureCount_WidensMeasureSpacing()
    {
        var timeSignature = new TimeSignature(4, new NoteValue(4));

        float defaultNextMeasure = GrandStaffLayout.MapScoreOnsetToX(1, 0, timeSignature, firstVisibleMeasure: 0);
        float narrowedNextMeasure = GrandStaffLayout.MapScoreOnsetToX(
            1,
            0,
            timeSignature,
            firstVisibleMeasure: 0,
            visibleMeasureCount: 2);

        Assert.NotEqual(defaultNextMeasure, narrowedNextMeasure);
        Assert.True(narrowedNextMeasure > defaultNextMeasure);
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

    [Fact]
    public void GetScoreNoteLayout_OctaveShiftedNote_UsesNotatedPitchPosition()
    {
        var shiftedNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 6),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            SoundingOctavesAboveNotated: 1);
        var notatedNote = shiftedNote with
        {
            Pitch = new Pitch(NoteLetter.C, 0, 5),
            SoundingOctavesAboveNotated = 0,
        };

        var shiftedLayout = GrandStaffLayout.GetScoreNoteLayout(
            shiftedNote,
            new TimeSignature(4, new NoteValue(4)),
            0);
        var notatedLayout = GrandStaffLayout.GetScoreNoteLayout(
            notatedNote,
            new TimeSignature(4, new NoteValue(4)),
            0);

        Assert.NotNull(shiftedLayout);
        Assert.NotNull(notatedLayout);
        Assert.Equal(notatedLayout.Value.Position.Staff, shiftedLayout.Value.Position.Staff);
        Assert.Equal(notatedLayout.Value.Position.Y, shiftedLayout.Value.Position.Y);
        Assert.Equal(notatedLayout.Value.Position.NeedsAccidental, shiftedLayout.Value.Position.NeedsAccidental);
        Assert.Equal(
            notatedLayout.Value.Position.DiatonicPosition.DiatonicOffset,
            shiftedLayout.Value.Position.DiatonicPosition.DiatonicOffset);
        Assert.Equal(
            notatedLayout.Value.Position.LedgerLineYs.ToArray(),
            shiftedLayout.Value.Position.LedgerLineYs.ToArray());
        Assert.Empty(shiftedLayout.Value.Position.LedgerLineYs);
        Assert.NotEmpty(GrandStaffLayout.GetPosition(shiftedNote.Pitch, Staff.Treble).LedgerLineYs);
    }

    [Fact]
    public void GetScoreNoteLayout_OctaveShiftedNote_PreservesSoundingPitch()
    {
        var shiftedNote = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 6),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            SoundingOctavesAboveNotated: 1);

        _ = GrandStaffLayout.GetScoreNoteLayout(
            shiftedNote,
            new TimeSignature(4, new NoteValue(4)),
            0);

        Assert.Equal(new Pitch(NoteLetter.C, 0, 6), shiftedNote.Pitch);
        Assert.Equal(new Pitch(NoteLetter.C, 0, 5), GrandStaffLayout.GetNotatedPitch(shiftedNote));
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
    public void GetScoreBarlineXs_FiveMeasureWindow_FillsAvailableScoreWidth()
    {
        var barlines = GrandStaffLayout.GetScoreBarlineXs(firstVisibleMeasure: 0, measureCount: 5);

        Assert.Equal(6, barlines.Count);
        Assert.Equal(GrandStaffLayout.ScoreX0, barlines[0]);
        Assert.Equal(GrandStaffLayout.ScoreX1, barlines[^1]);
    }

    [Fact]
    public void MapAbsoluteBeatToScoreX_PlaybackCursor_MatchesMeasureOnsetMapping()
    {
        var signature = new TimeSignature(4, new NoteValue(4));

        float cursorX = GrandStaffLayout.MapAbsoluteBeatToScoreX(5, signature, firstVisibleMeasure: 0);

        // Beat 5 in 4/4 time is measure 1, beat offset 1 (1.25 measures into a 5-measure window):
        // ScoreX0 + (1.25 / 5) * (ScoreX1 - ScoreX0).
        Assert.Equal(-0.18f, cursorX, 5);
    }

    [Fact]
    public void GetLiveFirstVisibleMeasure_CurrentBeatInSecondGroup_ReturnsMeasureGroupStart()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        var currentTime = TimeSpan.FromSeconds(13);

        int firstVisibleMeasure = GrandStaffLayout.GetLiveFirstVisibleMeasure(currentTime, signature, tempo);

        Assert.Equal(5, firstVisibleMeasure);
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
    public void GetLiveNoteLayout_NoteReleasedAcrossMeasureGroupBoundary_ClampsOnsetToScoreArea()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan boundaryTime = MusicalTime.BeatsToDuration(
            GrandStaffLayout.DefaultVisibleMeasureCount * signature.Numerator,
            tempo);
        TimeSpan startTime = boundaryTime - TimeSpan.FromMilliseconds(250);
        TimeSpan releaseTime = boundaryTime + TimeSpan.FromMilliseconds(250);

        var layout = GrandStaffLayout.GetLiveNoteLayout(
            new Pitch(NoteLetter.C, 0, 4),
            startTime,
            releaseTime,
            releaseTime,
            signature,
            tempo);

        Assert.NotNull(layout);
        Assert.Equal(GrandStaffLayout.ScoreX0, layout.Value.X);
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
    public void GetLiveNoteSegmentLayouts_NoteWithinMeasure_ReturnsUntiedFragmentAtMappedBeatPositions()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan startTime = MusicalTime.BeatsToDuration(1, tempo);
        TimeSpan endTime = MusicalTime.BeatsToDuration(3, tempo);
        var pitch = new Pitch(NoteLetter.C, 0, 4);

        var segment = Assert.Single(GrandStaffLayout.GetLiveNoteSegmentLayouts(
            pitch,
            startTime,
            endTime,
            endTime,
            signature,
            tempo));

        Assert.Equal(GrandStaffLayout.MapAbsoluteBeatToScoreX(1, signature, firstVisibleMeasure: 0), segment.X);
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(3, signature, firstVisibleMeasure: 0),
            segment.DurationEndX);
        Assert.Equal(1, segment.StartBeat);
        Assert.Equal(3, segment.EndBeat);
        Assert.False(segment.HasIncomingTie);
        Assert.False(segment.HasOutgoingTie);
    }

    [Fact]
    public void GetLiveNoteSegmentLayouts_NoteCrossingInternalBarlines_ReturnsTiedFragmentsPerMeasure()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan startTime = MusicalTime.BeatsToDuration(3, tempo);
        TimeSpan endTime = MusicalTime.BeatsToDuration(9, tempo);

        var segments = GrandStaffLayout.GetLiveNoteSegmentLayouts(
            new Pitch(NoteLetter.C, 0, 4),
            startTime,
            endTime,
            endTime,
            signature,
            tempo);

        Assert.Equal(3, segments.Count);
        Assert.Equal([3d, 4d, 8d], segments.Select(segment => segment.StartBeat));
        Assert.Equal([4d, 8d, 9d], segments.Select(segment => segment.EndBeat));
        Assert.Equal(
            GrandStaffLayout.MapAbsoluteBeatToScoreX(4, signature, firstVisibleMeasure: 0),
            segments[0].DurationEndX);
        Assert.Equal(segments[0].DurationEndX, segments[1].X);
        Assert.Equal(segments[1].DurationEndX, segments[2].X);
        Assert.False(segments[0].HasIncomingTie);
        Assert.True(segments[0].HasOutgoingTie);
        Assert.True(segments[1].HasIncomingTie);
        Assert.True(segments[1].HasOutgoingTie);
        Assert.True(segments[2].HasIncomingTie);
        Assert.False(segments[2].HasOutgoingTie);
    }

    [Fact]
    public void GetLiveNoteSegmentLayouts_NoteBeginningBeforeWindow_ReturnsVisibleIncomingContinuation()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan currentTime = MusicalTime.BeatsToDuration(21, tempo);

        var segments = GrandStaffLayout.GetLiveNoteSegmentLayouts(
            new Pitch(NoteLetter.C, 0, 4),
            MusicalTime.BeatsToDuration(19, tempo),
            currentTime,
            currentTime,
            signature,
            tempo);

        var segment = Assert.Single(segments);
        Assert.Equal(20, segment.StartBeat);
        Assert.Equal(21, segment.EndBeat);
        Assert.Equal(GrandStaffLayout.ScoreX0, segment.X);
        Assert.True(segment.HasIncomingTie);
        Assert.False(segment.HasOutgoingTie);
        Assert.All(segments, item => Assert.True(item.X >= GrandStaffLayout.ScoreX0));
    }

    [Fact]
    public void GetLiveNoteSegmentLayouts_ReleaseExactlyOnBarline_OmitsEmptyContinuationAndOutgoingTie()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan startTime = MusicalTime.BeatsToDuration(3, tempo);
        TimeSpan endTime = MusicalTime.BeatsToDuration(4, tempo);

        var segment = Assert.Single(GrandStaffLayout.GetLiveNoteSegmentLayouts(
            new Pitch(NoteLetter.C, 0, 4),
            startTime,
            endTime,
            endTime,
            signature,
            tempo));

        Assert.Equal(3, segment.StartBeat);
        Assert.Equal(4, segment.EndBeat);
        Assert.False(segment.HasOutgoingTie);
    }

    [Fact]
    public void GetLiveNoteSegmentLayouts_NoteOutsideWindow_ReturnsEmptyCollection()
    {
        var signature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        TimeSpan currentTime = MusicalTime.BeatsToDuration(21, tempo);

        var segments = GrandStaffLayout.GetLiveNoteSegmentLayouts(
            new Pitch(NoteLetter.C, 0, 4),
            MusicalTime.BeatsToDuration(1, tempo),
            MusicalTime.BeatsToDuration(2, tempo),
            currentTime,
            signature,
            tempo);

        Assert.Empty(segments);
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
            firstVisibleMeasure + GrandStaffLayout.DefaultVisibleMeasureCount);
        var actualBarlineXs = gridLines
            .Where(line => line.Kind == GridLineKind.Barline)
            .Select(line => line.X)
            .ToArray();
        Assert.Equal(expectedBarlineXs, actualBarlineXs);
    }

    [Theory]
    [InlineData(4, 15)]
    [InlineData(3, 10)]
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

        int visibleBeatCount = GrandStaffLayout.DefaultVisibleMeasureCount * numerator;
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
