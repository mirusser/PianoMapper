import { getAnalyserNode, getCurrentTime, isAudioActive } from "./audio.js";
import { grandStaffBraceAspect, grandStaffBraceOutline } from "./grand-staff-brace.js";

const canvases = new Map();

// Scene contract: every ordinal below is matched by hand against a C# enum, because this scene
// object crosses the JS interop seam as plain numbers with no shared source of truth. Changing
// either side's ordinals without the other breaks rendering silently — see
// PianoMapper.Tests/UnitTests/GrandStaffSceneContractTests.cs and this file's own
// scene-contract.test.mjs for the pinning tests that catch that drift.
export const grandStaffSceneKind = 0; // PianoMapper.Web.Rendering.PianoCanvasSceneKind.GrandStaff
export const pianoRollSceneKind = 1; // PianoCanvasSceneKind.PianoRoll
export const staffLineKind = 0; // PianoMapper.Web.Rendering.GrandStaffLineKind.Staff
export const ledgerLineKind = 1; // GrandStaffLineKind.Ledger
export const barlineKind = 2; // GrandStaffLineKind.Barline
export const cursorLineKind = 3; // GrandStaffLineKind.Cursor
export const beatLineKind = 4; // GrandStaffLineKind.Beat
export const glissandoLineKind = 5; // GrandStaffLineKind.Glissando
export const octaveShiftLineKind = 6; // GrandStaffLineKind.OctaveShift
export const braceLineKind = 7; // GrandStaffLineKind.Brace
export const finalBarlineKind = 8; // GrandStaffLineKind.FinalBarline
export const clefGlyphKind = 0; // PianoMapper.Web.Rendering.GrandStaffGlyphKind.Clef
export const accidentalGlyphKind = 1; // GrandStaffGlyphKind.Accidental
export const octaveShiftNumeralGlyphKind = 9; // GrandStaffGlyphKind.OctaveShiftNumeral
export const restGlyphKind = 10; // GrandStaffGlyphKind.Rest (drawn by the generic height-scaled glyph path)
// Review marks are their own channel (GrandStaffNote.reviewMark), never a Verdict: do not index verdictColors with them.
export const reviewMarkClean = 0; // PianoMapper.Web.Rendering.ReviewMark.Clean
export const reviewMarkTiming = 1; // ReviewMark.Timing
export const reviewMarkPitch = 2; // ReviewMark.Pitch
export const reviewMarkMissed = 3; // ReviewMark.Missed
export const stemDirectionUp = 0; // PianoMapper.Rendering.StemDirection.Up (also used for GrandStaffTie.CurveDirection)
// Index i must hold the color for PianoMapper.Core.Practice.Verdict's i-th ordinal.
export const verdictColors = [
    "#4ade80", // Correct
    "#f87171", // WrongPitch
    "#fb923c", // Early
    "#fb923c", // Late
    "#facc15", // TooShort
    "#facc15", // TooLong
    "#94a3b8", // Missed
    "#f472b6", // Extra
];
// Mirrors PianoMapper.Core/Rendering/GrandStaffLayout.cs's ScoreX0/ScoreX1 constants, so the
// score-playback cursor can be positioned here every animation frame from the Web Audio clock,
// instead of C# rebuilding the whole grand-staff scene every tick just to move the cursor line.
// The visible-measure count itself is NOT duplicated here — it arrives per call, either on the
// ScoreCursorPlaybackState pushed from Piano.razor (cursor.visibleMeasureCount) or as an explicit
// parameter below, so a user-configured count never drifts out of sync with this file the way a
// second hardcoded copy would. defaultScoreCursorVisibleMeasureCount is only a defensive fallback
// for a stale cached module that predates this field.
const scoreCursorX0 = -0.56;
const scoreCursorX1 = 0.96;
const defaultScoreCursorVisibleMeasureCount = 5;
const scoreNoteEdgeClearance = 0.02;
const defaultStaffSpace = 11;
const noteHeadWidthInStaffSpaces = 1.2;
const noteHeadHeightInStaffSpaces = 0.8;
const ledgerLineWidthInStaffSpaces = 2;
const noteHeadRotationRadians = -Math.PI / 8;
// A stem is drawn slightly inside the rotated oval. This avoids the hairline gap that appears
// when a vertical line starts at the unrotated horizontal radius rather than at the oval's ink.
const stemAttachmentOverlapInStaffSpaces = 0.04;
const stemLengthInStaffSpaces = 3;
const flagControlWidthInStaffSpaces = 1.2;
const flagControlHeightInStaffSpaces = 0.5;
const flagHeightInStaffSpaces = 1.15;
const flagSpacingInStaffSpaces = 0.45;
const dotClearanceFromStemInStaffSpaces = 0.3;
// Review marks (the halo): a ring around the heads of one prompt on one staff. The ring stays outside the head by
// this much (never less than the pixel floor, so it still clears the head on a very short canvas).
const reviewMarkHaloPaddingInStaffSpaces = 0.4;
const reviewMarkHaloMinimumPaddingPixels = 3;
// An accidental belongs to its note, so the ring wraps it instead of cutting through it. GrandStaffSceneBuilder puts an
// accidental's glyph about 0.019 scene-X left of its head; its width is a fraction of its (ink) height.
const reviewMarkAccidentalSearchWidth = 0.05;
const reviewMarkAccidentalHalfWidthInGlyphHeights = 0.28;
const reviewMarkRestHalfWidthInGlyphHeights = 0.2;
// Clear pixels kept between a ring's outer edge and an annotation strip or the canvas edge.
const reviewMarkStripGapPixels = 1.5;
// The least gap kept when the head leaves no room for the preferred one.
const reviewMarkStripHardGapPixels = 0.25;
// Own colors and line styles, not verdictColors (D3): shape carries the meaning as well as color, so the marks still
// read without telling orange from red. Clean notes have no entry on purpose, so no ring is drawn for them.
const darkReviewMarkStyles = new Map([
    [reviewMarkTiming, { color: "#14b8a6", lineWidth: 2, dash: [] }],
    [reviewMarkPitch, { color: "#e879f9", lineWidth: 3, dash: [] }],
    [reviewMarkMissed, { color: "#d1d5db", lineWidth: 2, dash: [6, 4] }],
]);
const lightReviewMarkStyles = new Map([
    [reviewMarkTiming, { color: "#0f766e", lineWidth: 2, dash: [] }],
    [reviewMarkPitch, { color: "#a21caf", lineWidth: 3, dash: [] }],
    [reviewMarkMissed, { color: "#475569", lineWidth: 2, dash: [6, 4] }],
]);
// Note-name labels are 16 px text centred under their note. Two labels in one row that would touch are drawn smaller (see
// getNoteLabelFitScales), never below this fraction of their size and keeping this much air between neighbours.
const noteLabelFontSizePixels = 16;
const noteLabelMinimumGapPixels = 2;
const noteLabelMinimumFitScale = 0.5;
// Labels this close are the same moment drawn a little apart (a chord's notes, a live note and the one it overlaps), not a
// run of neighbours, and shrinking them cannot separate them, so they are left at their own size.
const noteLabelSameMomentPixels = 4;
const tieEndpointInsetInStaffSpaces = (noteHeadWidthInStaffSpaces / 2) + 0.2;
const tieTipBiasInStaffSpaces = 0.12;
const tieMinimumVisibleLengthInStaffSpaces = 0.35;
const tieMinimumHeightInStaffSpaces = 0.3;
const tieMaximumHeightInStaffSpaces = 0.45;
const tieHeightToLengthRatio = 0.04;
const tieCenterThicknessInStaffSpaces = 0.08;
// 0.08 staff space is only 0.6 px on the smallest score canvas (7.5 px staff space), which reads as a hairline. A tie is
// never thinner than this at its thickest point, so it stays visible there; taller canvases already exceed it and are
// unchanged. The taper (zero thickness at both ends) and the endpoint gaps are not touched.
const tieMinimumCenterThicknessPixels = 1.2;
// A scene's X values are float-derived doubles (-0.56f arrives as -0.5600000023841858), so a position that must be
// recognised exactly is compared with this tolerance rather than Number.EPSILON.
const sceneXTolerance = 1e-6;
const slurMinimumHeightInStaffSpaces = 0.6;
const slurMaximumHeightInStaffSpaces = 1.6;
const slurHeightToLengthRatio = 0.12;
const slurStrokeWidthInStaffSpaces = 0.12;
const arpeggioMarkStrokeWidthInStaffSpaces = 0.12;
const arpeggioMarkWaveAmplitudeInStaffSpaces = 0.25;
const arpeggioMarkWaveSegmentHeightInStaffSpaces = 0.5;
const arpeggioMarkBracketTickWidthInStaffSpaces = 0.35;
const octaveShiftDashLengthInStaffSpaces = 0.7;
const octaveShiftDashGapInStaffSpaces = 0.45;
const staffLineWidth = 1.5;
const finalBarlineWidth = staffLineWidth * 2;
const grandStaffBraceGapPixels = 5;
const spectrumReleaseClearMilliseconds = 120;
const scoreNoteHitRadiusPixels = 12;
const plotLeftMargin = 44;
const plotRightMargin = 16;
const plotTopMargin = 26;
const plotBottomMargin = 34;

const darkGrandStaffPalette = {
    activeNote: "#22d3ee",
    arpeggio: "#e2e8f0",
    band: "rgba(51, 65, 85, 0.35)",
    barline: "#64748b",
    beam: "#60a5fa",
    beatLine: "#334155",
    clefGlyph: "#e2e8f0",
    cursor: "#fb7185",
    fingering: "#f8fafc",
    glissando: "#f472b6",
    glyph: "#f8fafc",
    ledgerLine: "#cbd5e1",
    note: "#60a5fa",
    octaveShift: "#60a5fa",
    reviewMarkStyles: darkReviewMarkStyles,
    scoreNoteSelection: "#38bdf8",
    scorePlaybackHighlight: "#a78bfa",
    slur: "#e2e8f0",
    staffLine: "#94a3b8",
    tie: "#60a5fa",
    activeTie: "#22d3ee",
    verdictColors,
};

const lightGrandStaffPalette = {
    activeNote: "#0369a1",
    arpeggio: "#111827",
    background: "#fff",
    band: "transparent",
    barline: "#111827",
    beam: "#111827",
    beatLine: "#9ca3af",
    clefGlyph: "#111827",
    cursor: "#dc2626",
    fingering: "#111827",
    glissando: "#111827",
    glyph: "#111827",
    ledgerLine: "#111827",
    note: "#111827",
    octaveShift: "#111827",
    reviewMarkStyles: lightReviewMarkStyles,
    scoreNoteSelection: "#0369a1",
    scorePlaybackHighlight: "#6d28d9",
    slur: "#111827",
    staffLine: "#111827",
    tie: "#111827",
    activeTie: "#0369a1",
    verdictColors: [
        "#15803d", // Correct
        "#b91c1c", // WrongPitch
        "#c2410c", // Early
        "#c2410c", // Late
        "#a16207", // TooShort
        "#a16207", // TooLong
        "#475569", // Missed
        "#a21caf", // Extra
    ],
};

export function initialize(canvas, waveformCanvas, spectrumCanvas, analysisLayout, scoreCursorElement) {
    initializeCanvas(canvas, waveformCanvas, spectrumCanvas, analysisLayout, scoreCursorElement);
}

export function initializeScoreCanvas(canvas, scoreCursorElement) {
    initializeCanvas(canvas, undefined, undefined, undefined, scoreCursorElement);
}

function initializeCanvas(canvas, waveformCanvas, spectrumCanvas, analysisLayout, scoreCursorElement) {
    dispose(canvas);

    const state = {
        canvas,
        waveformCanvas,
        spectrumCanvas,
        scene: { kind: grandStaffSceneKind, lines: [], glyphs: [], notes: [], beams: [], ties: [] },
        scoreLayerCanvas: document.createElement("canvas"),
        scoreLayerDirty: true,
        scoreLayerWidth: undefined,
        scoreLayerHeight: undefined,
        scoreLayerPixelRatio: undefined,
        scoreCursor: undefined,
        liveGrandStaffCursor: undefined,
        scoreCursorElement,
        scoreCanvasWidth: undefined,
        scoreCanvasHeight: undefined,
        scorePlaybackHighlightKey: undefined,
        canvasDrawRequested: false,
        scoreOverlay: undefined,
        selectedScoreNoteAddress: undefined,
        cachedStaffSpaceScene: undefined,
        cachedStaffSpaceHeight: undefined,
        cachedStaffSpace: undefined,
        cachedSelectedNoteScene: undefined,
        cachedSelectedNoteAddress: undefined,
        cachedSelectedNote: undefined,
        animationFrame: undefined,
        lastAudioActiveTimeMilliseconds: undefined,
        analyser: undefined,
        timeDomainData: undefined,
        frequencyData: undefined,
        analysisLayout,
        isWaveformVisible: false,
        isFrequencySpectrumVisible: false,
        isLightMode: false,
    };
    state.resizeObserver = new ResizeObserver(() => draw(state));
    state.resizeObserver.observe(canvas);
    if (waveformCanvas) {
        state.resizeObserver.observe(waveformCanvas);
    }
    if (spectrumCanvas) {
        state.resizeObserver.observe(spectrumCanvas);
    }
    canvases.set(canvas, state);
    draw(state);
}

export function render(canvas, scene, isWaveformVisible, isFrequencySpectrumVisible, selectedScoreNoteAddress, isLightMode = false) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.scene = scene;
    state.selectedScoreNoteAddress = selectedScoreNoteAddress;
    state.isWaveformVisible = isWaveformVisible;
    state.isFrequencySpectrumVisible = isFrequencySpectrumVisible;
    const nextIsLightMode = isLightMode === true;
    if (state.isLightMode !== nextIsLightMode) {
        state.isLightMode = nextIsLightMode;
        state.scoreLayerDirty = true;
    }
    if (scene.kind !== grandStaffSceneKind) {
        state.scoreOverlay = undefined;
    }
    state.scoreLayerDirty = true;
    draw(state);
}

export function updateScoreOverlay(canvas, overlay) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.scoreOverlay = overlay ?? undefined;
    requestDraw(state);
}

export function updateScoreSelection(canvas, selectedScoreNoteAddress) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.selectedScoreNoteAddress = selectedScoreNoteAddress;
    draw(state);
}

export function updateAnalysisVisibility(canvas, isWaveformVisible, isFrequencySpectrumVisible) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.isWaveformVisible = isWaveformVisible;
    state.isFrequencySpectrumVisible = isFrequencySpectrumVisible;
    draw(state);
}

export function hitTestScoreNote(canvas, offsetX, offsetY) {
    const state = canvases.get(canvas);
    if (!state
        || state.scene.kind !== grandStaffSceneKind
        || !Number.isFinite(offsetX)
        || !Number.isFinite(offsetY)) {
        return null;
    }

    const bounds = canvas.getBoundingClientRect();
    if (bounds.width <= 0 || bounds.height <= 0) {
        return null;
    }

    const staffSpace = getStaffSpace(state.scene, bounds.height);
    const radiusX = Math.max(scoreNoteHitRadiusPixels, staffSpace * noteHeadWidthInStaffSpaces / 2);
    const radiusY = Math.max(scoreNoteHitRadiusPixels, staffSpace * noteHeadHeightInStaffSpaces / 2);
    let closestAddress = null;
    let closestDistance = Number.POSITIVE_INFINITY;
    for (const note of state.scene.notes) {
        if (!isScoreNoteAddress(note.address)) {
            continue;
        }

        const deltaX = (offsetX - mapX(note.x, bounds.width)) / radiusX;
        const deltaY = (offsetY - mapY(note.y, bounds.height)) / radiusY;
        const distance = (deltaX * deltaX) + (deltaY * deltaY);
        if (distance <= 1 && distance < closestDistance) {
            closestAddress = note.address;
            closestDistance = distance;
        }
    }

    return closestAddress;
}

// `cursor` is a ScoreCursorPlaybackState pushed from Piano.razor whenever score playback starts,
// stops, completes, or the visible measure window changes. Its X position is then recomputed
// from the Web Audio clock on every animation frame in drawScoreCursor(), fully independently of
// any further C#/interop calls.
export function startScoreCursor(canvas, cursor) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.scoreCursor = cursor;
    draw(state);
}

export function stopScoreCursor(canvas) {
    const state = canvases.get(canvas);
    if (!state) {
        return;
    }

    state.scoreCursor = undefined;
    if (state.animationFrame !== undefined) {
        cancelAnimationFrame(state.animationFrame);
        state.animationFrame = undefined;
    }

    draw(state);
}

export function startLiveGrandStaffCursor(canvas, cursor) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.liveGrandStaffCursor = cursor;
    draw(state);
}

export function stopLiveGrandStaffCursor(canvas) {
    const state = canvases.get(canvas);
    if (!state) {
        return;
    }

    state.liveGrandStaffCursor = undefined;
    if (!state.scoreCursor) {
        hideScoreCursorOverlay(state);
    }

    draw(state);
}

export function dispose(canvas) {
    const state = canvases.get(canvas);
    if (!state) {
        return;
    }

    state.resizeObserver.disconnect();
    if (state.animationFrame !== undefined) {
        cancelAnimationFrame(state.animationFrame);
    }

    hideScoreCursorOverlay(state);
    canvases.delete(canvas);
}

function draw(state, knownScorePlaybackBeats = undefined, knownLiveGrandStaffPlaybackBeats = undefined) {
    const { canvas, scene } = state;
    const surface = prepareCanvas(canvas);
    if (!surface) {
        return;
    }

    const { context, width, height, pixelRatio } = surface;
    state.scoreCanvasWidth = width;
    state.scoreCanvasHeight = height;
    state.canvasDrawRequested = false;

    let scorePlaybackBeats;
    let liveGrandStaffPlaybackBeats;
    if (scene.kind === pianoRollSceneKind) {
        drawPianoRoll(context, scene, width, height);
        state.scorePlaybackHighlightKey = undefined;
        hideScoreCursorOverlay(state);
    } else {
        const palette = getGrandStaffPalette(state);
        const scoreLayer = prepareScoreLayer(state, width, height, pixelRatio);
        context.drawImage(scoreLayer, 0, 0, width, height);
        const staffSpace = getCachedStaffSpace(state, height);
        drawScoreNoteSelection(
            context,
            state,
            scene,
            width,
            height,
            state.selectedScoreNoteAddress,
            staffSpace,
            palette);
        scorePlaybackBeats = knownScorePlaybackBeats ?? getScorePlaybackBeats(state);
        liveGrandStaffPlaybackBeats = knownLiveGrandStaffPlaybackBeats ??
            getLiveGrandStaffPlaybackBeats(state);
        state.scorePlaybackHighlightKey = drawScorePlaybackHighlights(
            context,
            scene,
            width,
            height,
            scorePlaybackBeats,
            staffSpace,
            palette);
        drawLedgerLines(context, scene, width, height, staffSpace, palette);
        drawScoreOverlay(context, state.scoreOverlay, width, height, staffSpace, palette);
        if (state.scoreCursorElement) {
            updateCompositedCursorOverlay(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats);
        } else {
            drawScoreCursor(context, state, width, height, scorePlaybackBeats, palette);
        }
    }

    if (state.isWaveformVisible) {
        drawOscilloscope(state);
    }
    if (state.isFrequencySpectrumVisible) {
        drawSpectrum(state);
    }
    ensureAnimation(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats);
}

function getGrandStaffPalette(state) {
    return state.isLightMode ? lightGrandStaffPalette : darkGrandStaffPalette;
}

function drawScoreOverlay(context, overlay, width, height, staffSpace, palette) {
    if (!overlay) {
        return;
    }

    if (overlay.cursor) {
        drawLine(context, overlay.cursor, width, height, staffSpace, palette);
    }
    for (const note of overlay.notes ?? []) {
        drawNote(context, note, width, height, staffSpace, undefined, undefined, palette);
    }
    drawLedgerLines(context, { lines: overlay.ledgerLines ?? [] }, width, height, staffSpace, palette);
}

function drawScoreNoteSelection(context, state, scene, width, height, selectedAddress, staffSpace, palette) {
    if (!isScoreNoteAddress(selectedAddress)) {
        return;
    }

    const selectedNote = getSelectedScoreNote(state, scene, selectedAddress);
    if (!selectedNote) {
        return;
    }

    const radiusX = (staffSpace * noteHeadWidthInStaffSpaces / 2) + 5;
    const radiusY = (staffSpace * noteHeadHeightInStaffSpaces / 2) + 5;
    context.strokeStyle = palette.scoreNoteSelection;
    context.lineWidth = 3;
    context.beginPath();
    context.ellipse(
        mapX(selectedNote.x, width),
        mapY(selectedNote.y, height),
        radiusX,
        radiusY,
        noteHeadRotationRadians,
        0,
        Math.PI * 2);
    context.stroke();
}

function getSelectedScoreNote(state, scene, selectedAddress) {
    if (state.cachedSelectedNoteScene === scene
        && scoreNoteAddressesEqual(state.cachedSelectedNoteAddress, selectedAddress)) {
        return state.cachedSelectedNote;
    }

    const selectedNote = scene.notes.find(note => scoreNoteAddressesEqual(note.address, selectedAddress));
    state.cachedSelectedNoteScene = scene;
    state.cachedSelectedNoteAddress = selectedAddress;
    state.cachedSelectedNote = selectedNote;
    return selectedNote;
}

function isScoreNoteAddress(address) {
    return address
        && Number.isInteger(address.measureIndex)
        && Number.isInteger(address.noteIndex);
}

function scoreNoteAddressesEqual(first, second) {
    return isScoreNoteAddress(first)
        && isScoreNoteAddress(second)
        && first.measureIndex === second.measureIndex
        && first.noteIndex === second.noteIndex;
}

function prepareScoreLayer(state, width, height, pixelRatio) {
    const layer = state.scoreLayerCanvas;
    if (!state.scoreLayerDirty
        && state.scoreLayerWidth === width
        && state.scoreLayerHeight === height
        && state.scoreLayerPixelRatio === pixelRatio) {
        return layer;
    }

    layer.width = Math.round(width * pixelRatio);
    layer.height = Math.round(height * pixelRatio);
    const context = layer.getContext("2d");
    context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
    context.clearRect(0, 0, width, height);
    drawGrandStaff(context, state, width, height);

    state.scoreLayerDirty = false;
    state.scoreLayerWidth = width;
    state.scoreLayerHeight = height;
    state.scoreLayerPixelRatio = pixelRatio;
    return layer;
}

function drawGrandStaff(context, state, width, height) {
    const { scene } = state;
    const staffSpace = getCachedStaffSpace(state, height);
    const palette = getGrandStaffPalette(state);
    if (palette.background) {
        context.fillStyle = palette.background;
        context.fillRect(0, 0, width, height);
    }
    for (const band of scene.bands ?? []) {
        drawBand(context, band, width, height, palette);
    }

    for (const line of scene.lines) {
        if (line.kind === ledgerLineKind) {
            continue;
        }

        drawLine(context, line, width, height, staffSpace, palette);
    }

    let clefRight;
    for (const glyph of scene.glyphs) {
        if (glyph.kind !== clefGlyphKind) {
            continue;
        }

        const glyphRight = drawGlyph(context, glyph, width, height, palette);
        if (Number.isFinite(glyphRight)) {
            clefRight = Math.max(clefRight ?? glyphRight, glyphRight);
        }
    }

    const shouldClipNoteElements = scene.shouldClipNotesAtClefs && Number.isFinite(clefRight);
    if (shouldClipNoteElements) {
        const clefGap = 8;
        const noteAreaX0 = Math.max(clefRight + clefGap, mapX(scoreCursorX0, width));
        context.save();
        context.beginPath();
        context.rect(noteAreaX0, 0, Math.max(0, width - noteAreaX0), height);
        context.clip();
    }

    for (const glyph of scene.glyphs) {
        if (glyph.kind !== clefGlyphKind) {
            drawGlyph(context, glyph, width, height, palette);
        }
    }

    // Under the ties and notes: a ring never covers the notation it surrounds.
    drawReviewMarks(context, scene, width, height, staffSpace, palette);

    for (const tie of scene.ties ?? []) {
        drawTie(context, tie, width, height, staffSpace, palette);
    }

    for (const slur of scene.slurs ?? []) {
        drawSlur(context, slur, width, height, staffSpace, palette);
    }

    for (const arpeggioMark of scene.arpeggioMarks ?? []) {
        drawArpeggioMark(context, arpeggioMark, width, height, staffSpace, palette);
    }

    const labelFitScales = getLabelFitScales(context, scene.notes, width);
    for (const note of scene.notes) {
        if (!note.isActive) {
            drawNote(context, note, width, height, staffSpace, undefined, labelFitScales.get(note), palette);
        }
    }
    for (const beam of scene.beams ?? []) {
        drawBeam(context, beam, width, height, staffSpace, palette);
    }
    for (const note of scene.notes) {
        if (note.isActive) {
            drawNote(context, note, width, height, staffSpace, undefined, labelFitScales.get(note), palette);
        }
    }
    drawLedgerLines(context, scene, width, height, staffSpace, palette);

    if (shouldClipNoteElements) {
        context.restore();
    }
}

// Draws the review-mark halos of a finished exercise: one ring per prompt per staff (notes sharing reviewMarkGroup, so a
// chord gets one ring around its whole stack), none for a clean note. The scene only carries marks in Review.
function drawReviewMarks(context, scene, width, height, staffSpace, palette) {
    const groups = new Map();
    scene.notes.forEach((note, index) => {
        const style = palette.reviewMarkStyles.get(note.reviewMark);
        if (!style) {
            return;
        }

        const key = Number.isInteger(note.reviewMarkGroup) ? note.reviewMarkGroup : `note-${index}`;
        const group = groups.get(key) ?? { style, indexes: [] };
        group.indexes.push(index);
        groups.set(key, group);
    });
    if (groups.size === 0) {
        return;
    }

    const accidentals = (scene.glyphs ?? []).filter(glyph => glyph.kind === accidentalGlyphKind);
    const inkBoxes = scene.notes.map(note => ({
        ...getReviewMarkInkBox(note, accidentals, width, height, staffSpace),
        hasHalo: palette.reviewMarkStyles.has(note.reviewMark),
    }));
    const fixedObstacles = getReviewMarkFixedObstacles(scene, width, height);
    for (const group of groups.values()) {
        const members = new Set(group.indexes);
        drawReviewMarkHalo(
            context,
            group.style,
            group.indexes.map(index => inkBoxes[index]),
            [...fixedObstacles, ...inkBoxes.filter((_, index) => !members.has(index))],
            scene.bands ?? [],
            width,
            height,
            staffSpace);
    }
}

// Notation a ring must stay off that is not another note: barlines (a first or last note sits only a few pixels from one)
// and rests, whose glyph is a narrow shape centered on its beat.
function getReviewMarkFixedObstacles(scene, width, height) {
    const obstacles = [];
    for (const line of scene.lines) {
        if (line.kind === barlineKind) {
            const x = mapX(line.x0, width);
            obstacles.push({ left: x - 0.75, right: x + 0.75, top: Number.NEGATIVE_INFINITY, bottom: Number.POSITIVE_INFINITY, hasHalo: false });
        }
    }

    for (const glyph of scene.glyphs ?? []) {
        if (glyph.kind === restGlyphKind) {
            const x = mapX(glyph.x, width);
            const y = mapY(glyph.y, height);
            const glyphHeight = mapHeight(glyph.height, height);
            obstacles.push({
                left: x - (glyphHeight * reviewMarkRestHalfWidthInGlyphHeights),
                right: x + (glyphHeight * reviewMarkRestHalfWidthInGlyphHeights),
                top: y - (glyphHeight / 2),
                bottom: y + (glyphHeight / 2),
                hasHalo: false,
            });
        }
    }

    return obstacles;
}

// The ink of one note a halo has to respect: its head, plus its accidental, which belongs to the note and is drawn left
// of it (GrandStaffSceneBuilder puts it at the note's Y, a fixed small distance to the left).
function getReviewMarkInkBox(note, accidentals, width, height, staffSpace) {
    const x = mapX(note.x, width);
    const y = mapY(note.y, height);
    const box = {
        left: x - (staffSpace * noteHeadWidthInStaffSpaces / 2),
        right: x + (staffSpace * noteHeadWidthInStaffSpaces / 2),
        top: y - (staffSpace * noteHeadHeightInStaffSpaces / 2),
        bottom: y + (staffSpace * noteHeadHeightInStaffSpaces / 2),
    };
    const accidental = accidentals.find(glyph => Math.abs(glyph.y - note.y) < 1e-9
        && glyph.x < note.x
        && note.x - glyph.x <= reviewMarkAccidentalSearchWidth);
    if (accidental) {
        const accidentalHeight = mapHeight(accidental.height, height);
        box.left = Math.min(box.left, mapX(accidental.x, width) - (accidentalHeight * reviewMarkAccidentalHalfWidthInGlyphHeights));
        box.top = Math.min(box.top, y - (accidentalHeight / 2));
        box.bottom = Math.max(box.bottom, y + (accidentalHeight / 2));
    }

    return box;
}

function drawReviewMarkHalo(context, style, memberBoxes, otherBoxes, bands, width, height, staffSpace) {
    const { color, lineWidth, dash } = style;
    const preferredPadding = Math.max(reviewMarkHaloMinimumPaddingPixels, staffSpace * reviewMarkHaloPaddingInStaffSpaces);
    // The ring has to clear the ink it surrounds, so its centerline never sits closer than half its stroke plus a hair.
    const minimumPadding = (lineWidth / 2) + 0.5;
    let left = Math.min(...memberBoxes.map(box => box.left));
    let right = Math.max(...memberBoxes.map(box => box.right));
    let top = Math.min(...memberBoxes.map(box => box.top));
    let bottom = Math.max(...memberBoxes.map(box => box.bottom));

    // Beside another note (a beamed eighth pair is only a notehead and a half apart) the ring stays off that note's head
    // and accidental, and when the neighbour has a ring of its own the two share the free space between them.
    let roomLeft = Number.POSITIVE_INFINITY;
    let roomRight = Number.POSITIVE_INFINITY;
    for (const other of otherBoxes) {
        if (other.bottom < top - preferredPadding || other.top > bottom + preferredPadding) {
            continue;
        }

        const share = other.hasHalo ? 2 : 1;
        if (other.left >= right) {
            roomRight = Math.min(roomRight, (other.left - right) / share);
        } else if (other.right <= left) {
            roomLeft = Math.min(roomLeft, (left - other.right) / share);
        }
    }

    const fit = room => Math.max(minimumPadding, Math.min(preferredPadding, room - (lineWidth / 2) - 0.5));
    left -= fit(roomLeft);
    right += fit(roomRight);
    top -= preferredPadding;
    bottom += preferredPadding;

    // The annotation strip is a hard boundary (lessons #23-#26): a ring never reaches into one, and never past the
    // canvas edge. A note sitting right on a strip gets a ring that stops short of it instead, hugging the head on that
    // side (the lowest treble note is only a few pixels above its strip), but it never draws over the head itself.
    const half = lineWidth / 2;
    const inkTop = Math.min(...memberBoxes.map(box => box.top));
    const inkBottom = Math.max(...memberBoxes.map(box => box.bottom));
    const centerY = (top + bottom) / 2;
    let lowestTop = 0;
    let highestBottom = Number.POSITIVE_INFINITY;
    for (const band of bands) {
        const bandTop = Math.min(mapY(band.y0, height), mapY(band.y1, height));
        const bandBottom = Math.max(mapY(band.y0, height), mapY(band.y1, height));
        if (right < mapX(band.x0, width) || left > mapX(band.x1, width)) {
            continue;
        }

        if (centerY <= bandTop) {
            highestBottom = Math.min(highestBottom, bandTop);
        } else if (centerY >= bandBottom) {
            lowestTop = Math.max(lowestTop, bandBottom);
        }
    }

    top = Math.max(top, lowestTop + half + reviewMarkStripGapPixels);
    bottom = Math.min(bottom, highestBottom - half - reviewMarkStripGapPixels);
    top = Math.min(top, inkTop - half);
    bottom = Math.max(bottom, inkBottom + half);
    top = Math.max(top, lowestTop + half + reviewMarkStripHardGapPixels);
    bottom = Math.min(bottom, highestBottom - half - reviewMarkStripHardGapPixels);

    context.strokeStyle = color;
    context.lineWidth = lineWidth;
    context.setLineDash(dash);
    context.beginPath();
    context.roundRect(left, top, right - left, bottom - top, Math.min(right - left, bottom - top) / 2);
    context.stroke();
    context.setLineDash([]);
}

function drawLedgerLines(context, scene, width, height, staffSpace, palette = darkGrandStaffPalette) {
    let hasLedgerLines = false;
    context.strokeStyle = palette.ledgerLine;
    context.lineWidth = 1.5;
    context.beginPath();
    for (const line of scene.lines ?? []) {
        if (line.kind === ledgerLineKind) {
            const centerX = mapX((line.x0 + line.x1) / 2, width);
            const halfWidth = staffSpace * ledgerLineWidthInStaffSpaces / 2;
            context.moveTo(centerX - halfWidth, mapY(line.y0, height));
            context.lineTo(centerX + halfWidth, mapY(line.y1, height));
            hasLedgerLines = true;
        }
    }
    if (hasLedgerLines) {
        context.stroke();
    }
}

function getCachedStaffSpace(state, height) {
    if (state.cachedStaffSpaceScene === state.scene && state.cachedStaffSpaceHeight === height) {
        return state.cachedStaffSpace;
    }

    const staffSpace = getStaffSpace(state.scene, height);
    state.cachedStaffSpaceScene = state.scene;
    state.cachedStaffSpaceHeight = height;
    state.cachedStaffSpace = staffSpace;
    return staffSpace;
}

function getStaffSpace(scene, height) {
    const staffLines = scene.lines.filter(line => line.kind === staffLineKind);
    return staffLines.length >= 2
        ? mapHeight(Math.abs(staffLines[1].y0 - staffLines[0].y0), height)
        : defaultStaffSpace;
}

function prepareCanvas(canvas) {
    const bounds = canvas.getBoundingClientRect();
    if (bounds.width <= 0 || bounds.height <= 0) {
        return undefined;
    }

    const pixelRatio = window.devicePixelRatio || 1;
    const pixelWidth = Math.round(bounds.width * pixelRatio);
    const pixelHeight = Math.round(bounds.height * pixelRatio);
    if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;
    }

    const context = canvas.getContext("2d");
    context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
    context.clearRect(0, 0, bounds.width, bounds.height);
    context.fillStyle = "#030712";
    context.fillRect(0, 0, bounds.width, bounds.height);
    return { context, width: bounds.width, height: bounds.height, pixelRatio };
}

function drawPlotFrame(context, x0, x1, y0, y1) {
    context.strokeStyle = "#334155";
    context.lineWidth = 1;
    context.strokeRect(x0, y1, x1 - x0, y0 - y1);
}

function drawPlotText(context, text, x, y, align = "left") {
    context.fillStyle = "#cbd5e1";
    context.font = "12px system-ui, sans-serif";
    context.textAlign = align;
    context.textBaseline = "middle";
    context.fillText(text, x, y);
}

function drawRotatedPlotText(context, text, x, y) {
    context.save();
    context.translate(x, y);
    context.rotate(-Math.PI / 2);
    drawPlotText(context, text, 0, 0, "center");
    context.restore();
}

function drawOscilloscope(state) {
    const surface = prepareCanvas(state.waveformCanvas);
    if (!surface) {
        return;
    }

    const { context, width, height } = surface;
    const x0 = plotLeftMargin;
    const x1 = width - plotRightMargin;
    const y0 = height - plotBottomMargin;
    const y1 = plotTopMargin;
    const centerY = (y0 + y1) / 2;

    drawPlotFrame(context, x0, x1, y0, y1);
    drawPlotText(context, "Waveform", x0, 14);
    drawPlotText(context, "time", (x0 + x1) / 2, height - 12, "center");
    drawRotatedPlotText(context, "amplitude", 14, centerY);
    drawPlotText(context, "+", x0 - 12, y1, "center");
    drawPlotText(context, "0", x0 - 12, centerY, "center");
    drawPlotText(context, "-", x0 - 12, y0, "center");

    context.strokeStyle = "#334155";
    context.lineWidth = 1;
    context.beginPath();
    context.moveTo(x0, centerY);
    context.lineTo(x1, centerY);
    context.stroke();

    const analyser = prepareAnalyser(state);
    if (!analyser) {
        return;
    }

    state.timeDomainData ??= new Uint8Array(analyser.fftSize);
    analyser.getByteTimeDomainData(state.timeDomainData);
    context.strokeStyle = "#38bdf8";
    context.lineWidth = 2;
    context.beginPath();
    for (let index = 0; index < state.timeDomainData.length; index++) {
        const x = x0 + (index / (state.timeDomainData.length - 1)) * (x1 - x0);
        const normalized = state.timeDomainData[index] / 255;
        const y = y1 + normalized * (y0 - y1);
        if (index === 0) {
            context.moveTo(x, y);
        } else {
            context.lineTo(x, y);
        }
    }

    context.stroke();
}

function drawSpectrum(state) {
    const surface = prepareCanvas(state.spectrumCanvas);
    if (!surface) {
        return;
    }

    const { context, width, height } = surface;
    const panelX0 = plotLeftMargin;
    const panelX1 = width - plotRightMargin;
    const panelY0 = height - plotBottomMargin;
    const panelY1 = plotTopMargin;

    drawPlotFrame(context, panelX0, panelX1, panelY0, panelY1);
    drawPlotText(context, "Frequency spectrum", panelX0, 14);
    drawPlotText(context, "low Hz", panelX0, height - 12);
    drawPlotText(context, "high Hz", panelX1, height - 12, "right");
    drawRotatedPlotText(context, "magnitude", 14, (panelY0 + panelY1) / 2);
    drawPlotText(context, "max", panelX0 - 14, panelY1, "center");
    drawPlotText(context, "0", panelX0 - 14, panelY0, "center");

    const analyser = prepareAnalyser(state);
    if (!analyser) {
        return;
    }

    state.frequencyData ??= new Uint8Array(analyser.frequencyBinCount);
    if (isAudioActive()) {
        analyser.getByteFrequencyData(state.frequencyData);
    } else {
        state.frequencyData.fill(0);
    }
    const layout = state.analysisLayout;
    const visibleCount = Math.min(layout.spectrumVisibleBinCount, state.frequencyData.length);
    let maximum = 0;
    for (let index = 0; index < visibleCount; index++) {
        maximum = Math.max(maximum, state.frequencyData[index]);
    }

    if (maximum === 0 || visibleCount === 0) {
        return;
    }

    const barWidth = (panelX1 - panelX0) / visibleCount;
    context.fillStyle = "#a78bfa";
    for (let index = 0; index < visibleCount; index++) {
        const normalizedHeight = state.frequencyData[index] / maximum;
        const heightPixels = normalizedHeight * (panelY0 - panelY1);
        context.fillRect(
            panelX0 + (index * barWidth),
            panelY0 - heightPixels,
            barWidth * 0.85,
            heightPixels);
    }
}

function prepareAnalyser(state) {
    const analyser = getAnalyserNode();
    if (!analyser) {
        return undefined;
    }

    if (state.analyser !== analyser) {
        state.analyser = analyser;
        state.timeDomainData = undefined;
        state.frequencyData = undefined;
    }

    return analyser;
}

function ensureAnimation(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats) {
    const audioActive = isAudioActive();
    const now = performance.now();
    if (audioActive) {
        state.lastAudioActiveTimeMilliseconds = now;
    }

    const isAnalysisVisible = state.isWaveformVisible || state.isFrequencySpectrumVisible;
    const shouldAnimate = Number.isFinite(scorePlaybackBeats)
        || Number.isFinite(liveGrandStaffPlaybackBeats)
        || (isAnalysisVisible
            && (audioActive
            || (state.lastAudioActiveTimeMilliseconds !== undefined
                && now - state.lastAudioActiveTimeMilliseconds < spectrumReleaseClearMilliseconds)));

    if (!shouldAnimate) {
        return;
    }

    requestAnimationFrameForState(state);
}

function requestDraw(state) {
    state.canvasDrawRequested = true;
    requestAnimationFrameForState(state);
}

function requestAnimationFrameForState(state) {
    if (state.animationFrame !== undefined) {
        return;
    }

    state.animationFrame = requestAnimationFrame(() => {
        state.animationFrame = undefined;
        drawAnimationFrame(state);
    });
}

function drawAnimationFrame(state) {
    const scorePlaybackBeats = getScorePlaybackBeats(state);
    const liveGrandStaffPlaybackBeats = getLiveGrandStaffPlaybackBeats(state);
    const shouldRedrawCanvas = state.canvasDrawRequested ||
        !state.scoreCursorElement ||
        state.isWaveformVisible ||
        state.isFrequencySpectrumVisible ||
        hasScorePlaybackHighlightsChanged(state, scorePlaybackBeats);
    if (shouldRedrawCanvas) {
        draw(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats);
        return;
    }

    updateCompositedCursorOverlay(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats);
    ensureAnimation(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats);
}

function drawPianoRoll(context, scene, width, height) {
    for (const bar of scene.bars) {
        const x0 = mapX(bar.rect.x0, width);
        const x1 = mapX(bar.rect.x1, width);
        const y0 = mapY(bar.rect.y0, height);
        const y1 = mapY(bar.rect.y1, height);
        context.fillStyle = bar.isActive ? "#22d3ee" : "#60a5fa";
        context.fillRect(
            Math.min(x0, x1),
            Math.min(y0, y1),
            Math.max(2, Math.abs(x1 - x0)),
            Math.max(2, Math.abs(y1 - y0)));
        context.font = "11px system-ui, sans-serif";
        context.fillText(bar.label, Math.max(x0, x1) + 4, (y0 + y1) / 2 + 4);
    }
}

function getScorePlaybackBeats(state) {
    const cursor = state.scoreCursor;
    if (!cursor) {
        return undefined;
    }

    let currentTime;
    try {
        currentTime = getCurrentTime();
    } catch {
        return undefined;
    }

    if (currentTime > cursor.completionSeconds) {
        return undefined;
    }

    return (currentTime - cursor.anchorSeconds) / 60 * cursor.beatsPerMinute;
}

function getLiveGrandStaffPlaybackBeats(state) {
    const cursor = state.liveGrandStaffCursor;
    if (!cursor) {
        return undefined;
    }

    try {
        return getCurrentTime() / 60 * cursor.beatsPerMinute;
    } catch {
        return undefined;
    }
}

function drawScorePlaybackHighlights(context, scene, width, height, scorePlaybackBeats, staffSpace, palette) {
    const highlightKey = getScorePlaybackHighlightKey(scene, scorePlaybackBeats);
    if (highlightKey === undefined) {
        return highlightKey;
    }

    const labelFitScales = getLabelFitScales(context, scene.notes, width);
    for (let index = 0; index < scene.notes.length; index++) {
        const note = scene.notes[index];
        if (Number.isFinite(note.scoreOnsetBeats)
            && Number.isFinite(note.scoreEndBeats)
            && scorePlaybackBeats >= note.scoreOnsetBeats
            && scorePlaybackBeats < note.scoreEndBeats) {
            drawNote(
                context,
                note,
                width,
                height,
                staffSpace,
                palette.scorePlaybackHighlight,
                labelFitScales.get(note),
                palette);
        }
    }

    return highlightKey;
}

function hasScorePlaybackHighlightsChanged(state, scorePlaybackBeats) {
    return state.scene.kind === grandStaffSceneKind &&
        getScorePlaybackHighlightKey(state.scene, scorePlaybackBeats) !== state.scorePlaybackHighlightKey;
}

function getScorePlaybackHighlightKey(scene, scorePlaybackBeats) {
    if (!Number.isFinite(scorePlaybackBeats)) {
        return undefined;
    }

    let highlightKey;
    for (let index = 0; index < scene.notes.length; index++) {
        const note = scene.notes[index];
        if (Number.isFinite(note.scoreOnsetBeats)
            && Number.isFinite(note.scoreEndBeats)
            && scorePlaybackBeats >= note.scoreOnsetBeats
            && scorePlaybackBeats < note.scoreEndBeats) {
            highlightKey = highlightKey === undefined ? `${index}` : `${highlightKey},${index}`;
        }
    }

    return highlightKey;
}

function drawScoreCursor(context, state, width, height, scorePlaybackBeats, palette) {
    const cursor = state.scoreCursor;
    if (!cursor || !Number.isFinite(scorePlaybackBeats)) {
        return;
    }

    const visibleMeasureCount = cursor.visibleMeasureCount ?? defaultScoreCursorVisibleMeasureCount;
    const beats = Math.max(0, scorePlaybackBeats);
    const measureStartBeats = cursor.measureStartBeats;
    const windowStartBeat = getMeasureStartBeat(
        measureStartBeats,
        cursor.beatsPerMeasure,
        cursor.firstVisibleMeasure);
    const lastMeasureIndex = Array.isArray(measureStartBeats)
        ? Math.min(cursor.firstVisibleMeasure + visibleMeasureCount, measureStartBeats.length - 1)
        : cursor.firstVisibleMeasure + visibleMeasureCount;
    const windowEndBeat = getMeasureStartBeat(measureStartBeats, cursor.beatsPerMeasure, lastMeasureIndex);
    if (beats < windowStartBeat || beats >= windowEndBeat) {
        return;
    }

    const x = mapScoreCursorBeatToX(cursor, beats, visibleMeasureCount, measureStartBeats);
    drawLine(
        context,
        { x0: x, y0: cursor.cursorY0, x1: x, y1: cursor.cursorY1, kind: cursorLineKind },
        width,
        height,
        getCachedStaffSpace(state, height),
        palette);
}

function updateScoreCursorOverlay(state, scorePlaybackBeats) {
    const cursorElement = state.scoreCursorElement;
    const cursor = state.scoreCursor;
    const width = state.scoreCanvasWidth;
    const height = state.scoreCanvasHeight;
    if (!cursorElement ||
        !cursor ||
        !Number.isFinite(scorePlaybackBeats) ||
        !Number.isFinite(width) ||
        !Number.isFinite(height)) {
        hideScoreCursorOverlay(state);
        return;
    }

    const visibleMeasureCount = cursor.visibleMeasureCount ?? defaultScoreCursorVisibleMeasureCount;
    const beats = Math.max(0, scorePlaybackBeats);
    const measureStartBeats = cursor.measureStartBeats;
    const windowStartBeat = getMeasureStartBeat(
        measureStartBeats,
        cursor.beatsPerMeasure,
        cursor.firstVisibleMeasure);
    const lastMeasureIndex = Array.isArray(measureStartBeats)
        ? Math.min(cursor.firstVisibleMeasure + visibleMeasureCount, measureStartBeats.length - 1)
        : cursor.firstVisibleMeasure + visibleMeasureCount;
    const windowEndBeat = getMeasureStartBeat(measureStartBeats, cursor.beatsPerMeasure, lastMeasureIndex);
    if (beats < windowStartBeat || beats >= windowEndBeat) {
        hideScoreCursorOverlay(state);
        return;
    }

    const x = mapX(
        mapScoreCursorBeatToX(cursor, beats, visibleMeasureCount, measureStartBeats),
        width);
    const y0 = mapY(cursor.cursorY0, height);
    const y1 = mapY(cursor.cursorY1, height);
    cursorElement.style.display = "block";
    cursorElement.style.height = `${Math.abs(y1 - y0)}px`;
    cursorElement.style.transform = `translate3d(${x - 1}px, ${Math.min(y0, y1)}px, 0)`;
}

function updateCompositedCursorOverlay(state, scorePlaybackBeats, liveGrandStaffPlaybackBeats) {
    if (state.scoreCursor) {
        updateScoreCursorOverlay(state, scorePlaybackBeats);
        return;
    }

    updateLiveGrandStaffCursorOverlay(state, liveGrandStaffPlaybackBeats);
}

function updateLiveGrandStaffCursorOverlay(state, liveGrandStaffPlaybackBeats) {
    const cursorElement = state.scoreCursorElement;
    const cursor = state.liveGrandStaffCursor;
    const width = state.scoreCanvasWidth;
    const height = state.scoreCanvasHeight;
    if (!cursorElement ||
        !cursor ||
        !Number.isFinite(liveGrandStaffPlaybackBeats) ||
        !Number.isFinite(width) ||
        !Number.isFinite(height)) {
        hideScoreCursorOverlay(state);
        return;
    }

    const beats = Math.max(0, liveGrandStaffPlaybackBeats);
    const windowStartBeat = cursor.firstVisibleMeasure * cursor.beatsPerMeasure;
    const windowEndBeat = windowStartBeat + (defaultScoreCursorVisibleMeasureCount * cursor.beatsPerMeasure);
    if (beats < windowStartBeat || beats >= windowEndBeat) {
        hideScoreCursorOverlay(state);
        return;
    }

    const x = mapX(mapAbsoluteBeatToScoreX(beats, cursor.beatsPerMeasure, cursor.firstVisibleMeasure), width);
    const y0 = mapY(cursor.cursorY0, height);
    const y1 = mapY(cursor.cursorY1, height);
    cursorElement.style.display = "block";
    cursorElement.style.height = `${Math.abs(y1 - y0)}px`;
    cursorElement.style.transform = `translate3d(${x - 1}px, ${Math.min(y0, y1)}px, 0)`;
}

function hideScoreCursorOverlay(state) {
    if (state.scoreCursorElement) {
        state.scoreCursorElement.style.display = "none";
    }
}

function mapScoreCursorBeatToX(cursor, beats, visibleMeasureCount, measureStartBeats) {
    const layout = (cursor.measureLayouts ?? []).find(candidate =>
        beats >= candidate.startBeat && beats < candidate.endBeat);
    if (!layout) {
        return mapScoreNotationBeatToX(
            beats,
            cursor.beatsPerMeasure,
            cursor.firstVisibleMeasure,
            visibleMeasureCount,
            measureStartBeats);
    }

    const beatOffset = beats - layout.startBeat;
    const fraction = getScoreCursorSpacingFraction(layout, beatOffset);
    return Math.min(
        Math.max(
            layout.noteAreaStartX + fraction * (layout.noteAreaEndX - layout.noteAreaStartX),
            layout.noteAreaStartX),
        layout.noteAreaEndX);
}

function getScoreCursorSpacingFraction(layout, beatOffset) {
    const anchorBeats = layout.spacingAnchorBeats;
    const anchorFractions = layout.spacingAnchorFractions;
    if (!Array.isArray(anchorBeats)
        || !Array.isArray(anchorFractions)
        || anchorBeats.length < 2
        || anchorBeats.length !== anchorFractions.length) {
        const measureLength = layout.endBeat - layout.startBeat;
        return measureLength > 0 ? beatOffset / measureLength : 0;
    }

    for (let index = 1; index < anchorBeats.length; index += 1) {
        if (beatOffset <= anchorBeats[index] || index === anchorBeats.length - 1) {
            const beat0 = anchorBeats[index - 1];
            const beat1 = anchorBeats[index];
            const span = beat1 - beat0;
            const progress = span <= 0 ? 0 : (beatOffset - beat0) / span;
            return anchorFractions[index - 1] + progress * (anchorFractions[index] - anchorFractions[index - 1]);
        }
    }

    return anchorFractions.at(-1);
}

// Mirrors GrandStaffLayout.MapAbsoluteBeatToScoreX / MapScoreOnsetToX.
export function mapAbsoluteBeatToScoreX(
    absoluteBeat,
    beatsPerMeasure,
    firstVisibleMeasure,
    visibleMeasureCount = defaultScoreCursorVisibleMeasureCount,
    measureStartBeats = undefined) {
    const firstMeasureStartBeat = getMeasureStartBeat(measureStartBeats, beatsPerMeasure, firstVisibleMeasure);
    return scoreCursorX0 + ((absoluteBeat - firstMeasureStartBeat) / beatsPerMeasure / visibleMeasureCount)
        * (scoreCursorX1 - scoreCursorX0);
}

// Mirrors GrandStaffSceneBuilder.MapScoreNotationBeatToX for note and cursor alignment.
export function mapScoreNotationBeatToX(
    absoluteBeat,
    beatsPerMeasure,
    firstVisibleMeasure,
    visibleMeasureCount = defaultScoreCursorVisibleMeasureCount,
    measureStartBeats = undefined) {
    const measureIndex = findMeasureIndex(absoluteBeat, beatsPerMeasure, measureStartBeats);
    const measureStartBeat = getMeasureStartBeat(measureStartBeats, beatsPerMeasure, measureIndex);
    const beatOffset = absoluteBeat - measureStartBeat;
    const measureStartX = mapAbsoluteBeatToScoreX(
        measureStartBeat, beatsPerMeasure, firstVisibleMeasure, visibleMeasureCount, measureStartBeats);
    const measureEndX = mapAbsoluteBeatToScoreX(
        getMeasureStartBeat(measureStartBeats, beatsPerMeasure, measureIndex + 1),
        beatsPerMeasure,
        firstVisibleMeasure,
        visibleMeasureCount,
        measureStartBeats);
    const noteAreaStartX = measureIndex === firstVisibleMeasure
        ? measureStartX
        : measureStartX + scoreNoteEdgeClearance;
    const noteAreaEndX = measureEndX - scoreNoteEdgeClearance;
    const x = noteAreaStartX + (beatOffset / beatsPerMeasure) * (noteAreaEndX - noteAreaStartX);
    return Math.min(Math.max(x, noteAreaStartX), noteAreaEndX);
}

function getMeasureStartBeat(measureStartBeats, beatsPerMeasure, measureIndex) {
    if (Array.isArray(measureStartBeats)
        && measureIndex >= 0
        && measureIndex < measureStartBeats.length
        && Number.isFinite(measureStartBeats[measureIndex])) {
        return measureStartBeats[measureIndex];
    }

    return measureIndex * beatsPerMeasure;
}

function findMeasureIndex(absoluteBeat, beatsPerMeasure, measureStartBeats) {
    if (Array.isArray(measureStartBeats) && measureStartBeats.length >= 2) {
        for (let measureIndex = 0; measureIndex < measureStartBeats.length - 1; measureIndex += 1) {
            if (absoluteBeat < measureStartBeats[measureIndex + 1]) {
                return measureIndex;
            }
        }

        return measureStartBeats.length - 2;
    }

    return Math.floor(absoluteBeat / beatsPerMeasure);
}

// A fit is a pure function of the notes and the canvas width, and playback highlights redraw labels every frame, so the
// last result is kept per notes array.
const labelFitCache = new WeakMap();

function getLabelFitScales(context, notes, width) {
    const cached = labelFitCache.get(notes);
    if (cached?.width === width) {
        return cached.scales;
    }

    const scales = getNoteLabelFitScales(context, notes, width);
    labelFitCache.set(notes, { width, scales });
    return scales;
}

/**
 * The factor (at most 1) each note's label font is shrunk by so that it keeps noteLabelMinimumGapPixels clear of the
 * labels beside it, measured with the real font. Labels are compared only within one label row (same labelY, so a
 * chord's stacked names never compare with each other), in order of x. For each neighbouring pair the labels may use
 * the distance between their centres, less the gap, so both get the same factor: the widest factor that fits that pair.
 * A label takes the smaller factor of its two pairs, and none goes below noteLabelMinimumFitScale: the labels stay in
 * their strip and stay readable, a crowded run just gets smaller text. Only notes that have to shrink get an entry.
 */
export function getNoteLabelFitScales(context, notes, width) {
    const rows = new Map();
    for (const note of notes) {
        if (!Number.isFinite(note.labelY) || typeof note.label !== "string" || note.label.length === 0) {
            continue;
        }

        const row = rows.get(note.labelY);
        if (row) {
            row.push(note);
        } else {
            rows.set(note.labelY, [note]);
        }
    }

    const scales = new Map();
    context.save();
    for (const row of rows.values()) {
        if (row.length < 2) {
            continue;
        }

        row.sort((first, second) => first.x - second.x);
        const centers = row.map(note => mapX(note.x, width));
        const textWidths = row.map(note => {
            const labelFontScale = Number.isFinite(note.labelFontScale) ? note.labelFontScale : 1;
            context.font = `${noteLabelFontSizePixels * labelFontScale}px system-ui, sans-serif`;
            return context.measureText(note.label).width;
        });
        for (let index = 1; index < row.length; index++) {
            const distance = centers[index] - centers[index - 1];
            const combinedHalfWidths = (textWidths[index] + textWidths[index - 1]) / 2;
            if (distance <= noteLabelSameMomentPixels || !(combinedHalfWidths > 0)) {
                continue;
            }

            const pairScale = (distance - noteLabelMinimumGapPixels) / combinedHalfWidths;
            if (pairScale < 1) {
                const fitted = Math.max(noteLabelMinimumFitScale, pairScale);
                for (const note of [row[index - 1], row[index]]) {
                    scales.set(note, Math.min(fitted, scales.get(note) ?? 1));
                }
            }
        }
    }

    context.restore();
    return scales;
}

function drawBand(context, band, width, height, palette = darkGrandStaffPalette) {
    const x0 = mapX(band.x0, width);
    const x1 = mapX(band.x1, width);
    const y0 = mapY(band.y0, height);
    const y1 = mapY(band.y1, height);
    context.fillStyle = palette.band;
    context.beginPath();
    context.roundRect(x0, Math.min(y0, y1), x1 - x0, Math.abs(y1 - y0), 6);
    context.fill();
}

function drawLine(context, line, width, height, staffSpace, palette = darkGrandStaffPalette) {
    if (line.kind === braceLineKind) {
        drawGrandStaffBrace(context, line, width, height, palette);
        return;
    }

    context.strokeStyle = line.kind === cursorLineKind
        ? palette.cursor
        : line.kind === beatLineKind
            ? palette.beatLine
            : line.kind === barlineKind || line.kind === finalBarlineKind
            ? palette.barline
            : line.kind === staffLineKind
                ? palette.staffLine
                : line.kind === glissandoLineKind
                    ? palette.glissando
                    : line.kind === octaveShiftLineKind ? palette.octaveShift : palette.ledgerLine;
    context.lineWidth = line.kind === finalBarlineKind
        ? finalBarlineWidth
        : line.kind === staffLineKind
            ? staffLineWidth
        : line.kind === cursorLineKind
            ? 2
            : line.kind === beatLineKind
                ? 1
                : line.kind === glissandoLineKind ? 2 : 1.5;
    let x0 = mapX(line.x0, width);
    let x1 = mapX(line.x1, width);
    if (line.kind === ledgerLineKind) {
        const centerX = (x0 + x1) / 2;
        const halfWidth = staffSpace * ledgerLineWidthInStaffSpaces / 2;
        x0 = centerX - halfWidth;
        x1 = centerX + halfWidth;
    }
    if (line.kind === octaveShiftLineKind) {
        context.setLineDash([
            staffSpace * octaveShiftDashLengthInStaffSpaces,
            staffSpace * octaveShiftDashGapInStaffSpaces,
        ]);
    }
    context.beginPath();
    context.moveTo(x0, mapY(line.y0, height));
    context.lineTo(x1, mapY(line.y1, height));
    context.stroke();
    if (line.kind === octaveShiftLineKind) {
        context.setLineDash([]);
    }
}

function drawGrandStaffBrace(context, line, width, height, palette = darkGrandStaffPalette) {
    const x = mapX(line.x0, width);
    const y0 = mapY(line.y0, height);
    const y1 = mapY(line.y1, height);
    const top = Math.min(y0, y1);
    const braceHeight = Math.max(y0, y1) - top;
    const left = x - grandStaffBraceGapPixels - (braceHeight * grandStaffBraceAspect);
    context.save();
    context.fillStyle = palette.staffLine;
    context.beginPath();
    for (let i = 0; i < grandStaffBraceOutline.length; i += 2) {
        const pointX = left + (grandStaffBraceOutline[i] * braceHeight);
        const pointY = top + (grandStaffBraceOutline[i + 1] * braceHeight);
        if (i === 0) {
            context.moveTo(pointX, pointY);
        } else {
            context.lineTo(pointX, pointY);
        }
    }
    context.closePath();
    context.fill();
    context.restore();
}

function drawGlyph(context, glyph, width, height, palette = darkGrandStaffPalette) {
    if (glyph.kind === accidentalGlyphKind) {
        context.fillStyle = Number.isInteger(glyph.verdict)
            ? palette.verdictColors[glyph.verdict]
            : glyph.isActive ? palette.activeNote : palette.note;
    } else {
        context.fillStyle = glyph.kind === clefGlyphKind ? palette.clefGlyph : palette.glyph;
    }
    context.textAlign = "center";
    const x = mapX(glyph.x, width);
    const y = mapY(glyph.y, height);
    if (glyph.height > 0) {
        const measurementFontSize = 100;
        context.font = `${measurementFontSize}px 'Noto Music', 'Bravura Text', serif`;
        const measurement = context.measureText(glyph.text);
        const measuredHeight = measurement.actualBoundingBoxAscent + measurement.actualBoundingBoxDescent;
        // A visually "flat" character (a dash/underscore-shaped mark, centered near the
        // baseline) measures a tiny actualBoundingBoxAscent+Descent relative to a normal
        // letter/digit/musical-symbol glyph — dividing by that near-zero height below would
        // blow the computed font size up by 10x+ and render as an oversized, overlapping blob
        // (found by screenshot-verifying the tenuto articulation mark, an en dash "–", against
        // the real running app — see docs/plans/notations-rendering.md). Floor the divisor at a
        // fraction of the reference size so a flat glyph renders at a comparable scale to its
        // neighbors instead of blowing up; every glyph kind already in use here (clef,
        // accidental, signatures, fermata, tuplet numerals) measures well above this floor, so
        // the clamp is a no-op for them.
        const minimumMeasuredHeight = measurementFontSize * 0.2;
        const targetHeight = mapHeight(glyph.height, height);
        const fontSize = measurementFontSize * targetHeight / Math.max(measuredHeight, minimumMeasuredHeight);
        context.font = `${fontSize}px 'Noto Music', 'Bravura Text', serif`;

        const metrics = context.measureText(glyph.text);
        context.textBaseline = "alphabetic";
        const baselineY = y + ((metrics.actualBoundingBoxAscent - metrics.actualBoundingBoxDescent) / 2);
        context.fillText(glyph.text, x, baselineY);
        return x + metrics.actualBoundingBoxRight;
    }

    context.font = "18px 'Noto Music', 'Bravura Text', serif";
    context.textBaseline = "middle";
    context.fillText(glyph.text, x, y);
}

function drawNote(
    context,
    note,
    width,
    height,
    staffSpace,
    colorOverride,
    labelFitScale = 1,
    palette = darkGrandStaffPalette) {
    const x = mapX(note.x, width);
    const y = mapY(note.y, height);
    const noteHeadRadiusX = staffSpace * noteHeadWidthInStaffSpaces / 2;
    const noteHeadRadiusY = staffSpace * noteHeadHeightInStaffSpaces / 2;
    const noteColor = colorOverride ?? (Number.isInteger(note.verdict)
        ? palette.verdictColors[note.verdict]
        : note.isActive ? palette.activeNote : palette.note);
    context.strokeStyle = noteColor;
    context.fillStyle = context.strokeStyle;
    context.lineWidth = 2;
    if (Number.isFinite(note.durationEndX)) {
        const durationEndX = mapX(note.durationEndX, width);
        if (durationEndX > x) {
            context.beginPath();
            context.moveTo(x, y);
            context.lineTo(durationEndX, y);
            context.stroke();
        }
    }

    context.beginPath();
    context.ellipse(x, y, noteHeadRadiusX, noteHeadRadiusY, noteHeadRotationRadians, 0, Math.PI * 2);
    if (note.isFilled) {
        context.fill();
    } else {
        context.stroke();
    }

    let stemEndY = y;
    let stemX = x;
    const stemGoesUp = note.stemDirection === stemDirectionUp;
    if (note.hasStem) {
        stemX = x + ((stemGoesUp ? 1 : -1) * getStemAttachmentOffset(
            noteHeadRadiusX,
            noteHeadRadiusY,
            staffSpace));
        stemEndY = Number.isFinite(note.stemEndY)
            ? mapY(note.stemEndY, height)
            : y + (stemGoesUp ? -1 : 1) * staffSpace * stemLengthInStaffSpaces;
        context.beginPath();
        context.moveTo(stemX, y);
        context.lineTo(stemX, stemEndY);
        context.stroke();
    }

    const flagXDirection = stemGoesUp ? 1 : -1;
    const flagYDirection = stemGoesUp ? 1 : -1;
    for (let flagIndex = 0; flagIndex < note.flagCount; flagIndex++) {
        const flagStartY = stemEndY
            + (flagIndex * staffSpace * flagSpacingInStaffSpaces * flagYDirection);
        context.beginPath();
        context.moveTo(stemX, flagStartY);
        context.quadraticCurveTo(
            stemX + (staffSpace * flagControlWidthInStaffSpaces * flagXDirection),
            flagStartY + (staffSpace * flagControlHeightInStaffSpaces * flagYDirection),
            stemX,
            flagStartY + (staffSpace * flagHeightInStaffSpaces * flagYDirection));
        context.stroke();
    }

    if (note.hasDot) {
        const dotRadius = Math.max(2, staffSpace * 0.08);
        // A stem-up note's stem stands on the right edge of its head, exactly where the dot goes, so the dot is moved
        // clear of the stem; otherwise it fuses with it and a dotted note reads as an undotted one.
        const dotX = note.hasStem && stemGoesUp
            ? stemX + (staffSpace * dotClearanceFromStemInStaffSpaces) + dotRadius
            : x + noteHeadRadiusX + (staffSpace * 0.25);
        context.beginPath();
        context.arc(dotX, y, dotRadius, 0, Math.PI * 2);
        context.fill();
    }

    if (Number.isFinite(note.labelY)) {
        // Shrinks in lockstep with GrandStaffSceneBuilder's compressed row spacing for a severely crowded
        // label stack (e.g. a 3-note chord) — 1 (full 16px) for the overwhelmingly common, uncompressed case.
        const labelFontScale = Number.isFinite(note.labelFontScale) ? note.labelFontScale : 1;
        // labelFitScale shrinks it further when a neighbouring label in the same row would touch this one.
        context.font = `${noteLabelFontSizePixels * labelFontScale * labelFitScale}px system-ui, sans-serif`;
        context.textAlign = "center";
        context.textBaseline = "top";
        context.fillText(note.label, x, mapY(note.labelY, height));
    }

    if (typeof note.fingering === "string" && Number.isFinite(note.fingeringY)) {
        context.fillStyle = palette.fingering;
        context.font = "600 15px system-ui, sans-serif";
        context.textBaseline = "middle";
        context.fillText(note.fingering, x, mapY(note.fingeringY, height));
    }
}

function getStemAttachmentOffset(noteHeadRadiusX, noteHeadRadiusY, staffSpace) {
    const sine = Math.sin(noteHeadRotationRadians);
    const cosine = Math.cos(noteHeadRotationRadians);
    const rotatedVerticalRadius = 1 / Math.sqrt(
        ((cosine * cosine) / (noteHeadRadiusX * noteHeadRadiusX)) +
        ((sine * sine) / (noteHeadRadiusY * noteHeadRadiusY)));
    return Math.max(0, rotatedVerticalRadius - (staffSpace * stemAttachmentOverlapInStaffSpaces));
}

function drawBeam(context, beam, width, height, staffSpace, palette = darkGrandStaffPalette) {
    const stemGoesUp = beam.stemDirection === stemDirectionUp;
    const noteHeadRadiusX = staffSpace * noteHeadWidthInStaffSpaces / 2;
    const noteHeadRadiusY = staffSpace * noteHeadHeightInStaffSpaces / 2;
    const stemXOffset = getStemAttachmentOffset(noteHeadRadiusX, noteHeadRadiusY, staffSpace) *
        (stemGoesUp ? 1 : -1);
    const beamSpacingDirection = stemGoesUp ? 1 : -1;
    const x0 = mapX(beam.x0, width) + stemXOffset;
    const x1 = mapX(beam.x1, width) + stemXOffset;
    const y0 = mapY(beam.y0, height);
    const y1 = mapY(beam.y1, height);
    context.strokeStyle = palette.beam;
    context.lineWidth = Math.max(3, staffSpace * 0.45);
    context.lineCap = "butt";
    const beamLevel = Number.isInteger(beam.level) && beam.level > 0 ? beam.level : 0;
    for (let beamIndex = 0; beamIndex < beam.count; beamIndex++) {
        const yOffset = (beamLevel + beamIndex) * staffSpace * 0.6 * beamSpacingDirection;
        context.beginPath();
        context.moveTo(x0, y0 + yOffset);
        context.lineTo(x1, y1 + yOffset);
        context.stroke();
    }
}

function drawTie(context, tie, width, height, staffSpace, palette = darkGrandStaffPalette) {
    const noteCenterX0 = mapX(tie.x0, width);
    const noteCenterX1 = mapX(tie.x1, width);
    const horizontalDirection = noteCenterX1 >= noteCenterX0 ? 1 : -1;
    const requestedStartInset = isScoreEdgeX(tie.x0)
        ? 0
        : staffSpace * tieEndpointInsetInStaffSpaces;
    const requestedEndInset = isScoreEdgeX(tie.x1)
        ? 0
        : staffSpace * tieEndpointInsetInStaffSpaces;
    const centerSpan = Math.abs(noteCenterX1 - noteCenterX0);
    const minimumVisibleLength = Math.min(
        centerSpan,
        staffSpace * tieMinimumVisibleLengthInStaffSpaces);
    const availableInset = Math.max(0, centerSpan - minimumVisibleLength);
    const requestedInset = requestedStartInset + requestedEndInset;
    const insetScale = requestedInset > 0
        ? Math.min(1, availableInset / requestedInset)
        : 0;
    const x0 = noteCenterX0 + (horizontalDirection * requestedStartInset * insetScale);
    const x1 = noteCenterX1 - (horizontalDirection * requestedEndInset * insetScale);
    const curveDirection = tie.curveDirection === stemDirectionUp ? -1 : 1;
    const tipBias = curveDirection * staffSpace * tieTipBiasInStaffSpaces;
    const y0 = mapY(tie.y0, height) + tipBias;
    const y1 = mapY(tie.y1, height) + tipBias;
    const lengthInStaffSpaces = Math.hypot(x1 - x0, y1 - y0) / staffSpace;
    const heightInStaffSpaces = Math.min(
        tieMaximumHeightInStaffSpaces,
        Math.max(tieMinimumHeightInStaffSpaces, lengthInStaffSpaces * tieHeightToLengthRatio));
    const controlHeight = curveDirection * staffSpace * heightInStaffSpaces * 4 / 3;
    const controlX1 = x0 + ((x1 - x0) / 3);
    const controlX2 = x0 + ((x1 - x0) * 2 / 3);
    const centerControlY1 = y0 + ((y1 - y0) / 3) + controlHeight;
    const centerControlY2 = y0 + ((y1 - y0) * 2 / 3) + controlHeight;
    const thicknessControlOffset = curveDirection
        * Math.max(staffSpace * tieCenterThicknessInStaffSpaces, tieMinimumCenterThicknessPixels)
        * 2 / 3;
    context.fillStyle = tie.isActive ? palette.activeTie : palette.tie;
    context.beginPath();
    context.moveTo(x0, y0);
    context.bezierCurveTo(
        controlX1,
        centerControlY1 + thicknessControlOffset,
        controlX2,
        centerControlY2 + thicknessControlOffset,
        x1,
        y1);
    context.bezierCurveTo(
        controlX2,
        centerControlY2 - thicknessControlOffset,
        controlX1,
        centerControlY1 - thicknessControlOffset,
        x0,
        y0);
    context.closePath();
    context.fill();
}

// A slur (a thin stroked bezier arc over a whole phrase) shares GrandStaffTie's X0/Y0/X1/Y1/
// direction shape but is drawn with its own simpler curve, not drawTie's tapered filled shape —
// see the "Curve rendering exists but is tuned for a different shape" note in
// docs/plans/notations-rendering.md.
function drawSlur(context, slur, width, height, staffSpace, palette = darkGrandStaffPalette) {
    const x0 = mapX(slur.x0, width);
    const x1 = mapX(slur.x1, width);
    const y0 = mapY(slur.y0, height);
    const y1 = mapY(slur.y1, height);
    const curveDirection = slur.curveDirection === stemDirectionUp ? -1 : 1;
    const lengthInStaffSpaces = Math.hypot(x1 - x0, y1 - y0) / staffSpace;
    const heightInStaffSpaces = Math.min(
        slurMaximumHeightInStaffSpaces,
        Math.max(slurMinimumHeightInStaffSpaces, lengthInStaffSpaces * slurHeightToLengthRatio));
    const controlHeight = curveDirection * staffSpace * heightInStaffSpaces;
    const controlX1 = x0 + ((x1 - x0) / 3);
    const controlX2 = x0 + ((x1 - x0) * 2 / 3);
    const controlY1 = y0 + ((y1 - y0) / 3) + controlHeight;
    const controlY2 = y0 + ((y1 - y0) * 2 / 3) + controlHeight;
    context.strokeStyle = palette.slur;
    context.lineWidth = Math.max(1, staffSpace * slurStrokeWidthInStaffSpaces);
    context.beginPath();
    context.moveTo(x0, y0);
    context.bezierCurveTo(controlX1, controlY1, controlX2, controlY2, x1, y1);
    context.stroke();
}

// A vertical arpeggio mark to the left of a chord: a wavy line (quadratic-curve zigzag) for
// GrandStaffArpeggioMark.IsNonArpeggiate false, or a straight bracket for true — no existing
// primitive to adapt (ties/beams/slurs are all horizontal-ish; this is the first vertical mark).
function drawArpeggioMark(context, mark, width, height, staffSpace, palette = darkGrandStaffPalette) {
    const x = mapX(mark.x, width);
    const yTop = mapY(Math.max(mark.y0, mark.y1), height);
    const yBottom = mapY(Math.min(mark.y0, mark.y1), height);
    context.strokeStyle = palette.arpeggio;
    context.lineWidth = Math.max(1, staffSpace * arpeggioMarkStrokeWidthInStaffSpaces);
    if (mark.isNonArpeggiate) {
        drawArpeggioBracket(context, x, yTop, yBottom, staffSpace);
    } else {
        drawArpeggioWave(context, x, yTop, yBottom, staffSpace);
    }
}

function drawArpeggioBracket(context, x, yTop, yBottom, staffSpace) {
    const tickWidth = staffSpace * arpeggioMarkBracketTickWidthInStaffSpaces;
    context.beginPath();
    context.moveTo(x + tickWidth, yTop);
    context.lineTo(x, yTop);
    context.lineTo(x, yBottom);
    context.lineTo(x + tickWidth, yBottom);
    context.stroke();
}

function drawArpeggioWave(context, x, yTop, yBottom, staffSpace) {
    const amplitude = staffSpace * arpeggioMarkWaveAmplitudeInStaffSpaces;
    const segmentHeight = staffSpace * arpeggioMarkWaveSegmentHeightInStaffSpaces;
    const totalHeight = Math.max(1, yBottom - yTop);
    const segmentCount = Math.max(1, Math.round(totalHeight / segmentHeight));
    const actualSegmentHeight = totalHeight / segmentCount;
    context.beginPath();
    context.moveTo(x, yTop);
    for (let segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++) {
        const segmentStartY = yTop + (segmentIndex * actualSegmentHeight);
        const segmentEndY = segmentStartY + actualSegmentHeight;
        const controlX = x + (segmentIndex % 2 === 0 ? amplitude : -amplitude);
        const controlY = segmentStartY + (actualSegmentHeight / 2);
        context.quadraticCurveTo(controlX, controlY, x, segmentEndY);
    }
    context.stroke();
}

function isScoreEdgeX(x) {
    return Math.abs(x - scoreCursorX0) < Number.EPSILON
        || Math.abs(x - scoreCursorX1) < Number.EPSILON
        // The half tie that arrives at the start of a score row starts at that row's opening barline, one note-edge
        // clearance left of scoreCursorX0 (GrandStaffSceneBuilder.OpeningBarlineLead), and has no gap before it.
        || Math.abs(x - (scoreCursorX0 - scoreNoteEdgeClearance)) < sceneXTolerance;
}

function mapX(value, width) {
    const padding = 18;
    return padding + ((value + 1) / 2) * Math.max(0, width - (padding * 2));
}

function mapY(value, height) {
    const padding = 18;
    return height - padding - ((value + 1) / 2) * Math.max(0, height - (padding * 2));
}

function mapHeight(value, height) {
    const padding = 18;
    return (value / 2) * Math.max(0, height - (padding * 2));
}
