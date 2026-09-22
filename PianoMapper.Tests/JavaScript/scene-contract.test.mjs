import assert from "node:assert/strict";
import test from "node:test";

// Pins the JS half of the scene contract that crosses the Blazor/JS interop seam in
// wwwroot/js/canvas.js. Canvas.js matches these values against C# enum ordinals by hand — there
// is no shared source of truth between the two languages. If a C# enum's ordinals change without
// canvas.js, the matching assertions in PianoMapper.Tests/UnitTests/GrandStaffSceneContractTests.cs
// stay green while these fail (or vice versa), which is the point: either side drifting alone
// shows up as a failure.
import {
    accidentalGlyphKind,
    barlineKind,
    beatLineKind,
    clefGlyphKind,
    cursorLineKind,
    grandStaffSceneKind,
    ledgerLineKind,
    mapAbsoluteBeatToScoreX,
    pianoRollSceneKind,
    staffLineKind,
    stemDirectionUp,
    verdictColors,
} from "../../PianoMapper.Web/wwwroot/js/canvas.js";

test("scene kind constants match PianoCanvasSceneKind ordinals", () => {
    assert.equal(grandStaffSceneKind, 0); // PianoCanvasSceneKind.GrandStaff
    assert.equal(pianoRollSceneKind, 1); // PianoCanvasSceneKind.PianoRoll
});

test("line kind constants match GrandStaffLineKind ordinals", () => {
    assert.equal(staffLineKind, 0);
    assert.equal(ledgerLineKind, 1);
    assert.equal(barlineKind, 2);
    assert.equal(cursorLineKind, 3);
    assert.equal(beatLineKind, 4);
});

test("clef glyph kind constant matches GrandStaffGlyphKind.Clef ordinal", () => {
    assert.equal(clefGlyphKind, 0);
});

test("accidental glyph kind constant matches GrandStaffGlyphKind.Accidental ordinal", () => {
    assert.equal(accidentalGlyphKind, 1);
});

test("stem direction up constant matches StemDirection.Up ordinal", () => {
    assert.equal(stemDirectionUp, 0);
});

test("verdict colors array has one entry per Verdict ordinal, in order", () => {
    // Must stay length 8, in Verdict's declared order: Correct, WrongPitch, Early, Late,
    // TooShort, TooLong, Missed, Extra. A new Verdict value needs a new entry appended here.
    assert.equal(verdictColors.length, 8);
});

test("score cursor X mapping mirrors GrandStaffLayout.ScoreX0/ScoreX1/VisibleMeasureCount", () => {
    // These three literals (-0.56, 0.96, 5) are canvas.js's own copy of
    // PianoMapper.Core/Rendering/GrandStaffLayout.cs's ScoreX0/ScoreX1/VisibleMeasureCount,
    // duplicated so the score-playback cursor can animate off the Web Audio clock without a
    // per-frame interop round trip (see the comment above scoreCursorX0 in canvas.js). If Core's
    // constants change, these expected values must change with them.
    const beatsPerMeasure = 4;
    assert.ok(Math.abs(mapAbsoluteBeatToScoreX(0, beatsPerMeasure, 0) - -0.56) < 1e-9);
    assert.ok(Math.abs(mapAbsoluteBeatToScoreX(20, beatsPerMeasure, 0) - 0.96) < 1e-9);
});
