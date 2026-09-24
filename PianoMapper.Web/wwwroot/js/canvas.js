import { getAnalyserNode, getCurrentTime, isAudioActive } from "./audio.js";

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
export const clefGlyphKind = 0; // PianoMapper.Web.Rendering.GrandStaffGlyphKind.Clef
export const accidentalGlyphKind = 1; // GrandStaffGlyphKind.Accidental
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
    "#c084fc", // Extra
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
const stemLengthInStaffSpaces = 3;
const flagControlWidthInStaffSpaces = 1.2;
const flagControlHeightInStaffSpaces = 0.5;
const flagHeightInStaffSpaces = 1.15;
const flagSpacingInStaffSpaces = 0.45;
const tieEndpointInsetInStaffSpaces = (noteHeadWidthInStaffSpaces / 2) + 0.2;
const tieTipBiasInStaffSpaces = 0.12;
const tieMinimumVisibleLengthInStaffSpaces = 0.35;
const tieMinimumHeightInStaffSpaces = 0.3;
const tieMaximumHeightInStaffSpaces = 0.45;
const tieHeightToLengthRatio = 0.04;
const tieCenterThicknessInStaffSpaces = 0.08;
const slurMinimumHeightInStaffSpaces = 0.6;
const slurMaximumHeightInStaffSpaces = 1.6;
const slurHeightToLengthRatio = 0.12;
const slurStrokeWidthInStaffSpaces = 0.12;
const slurColor = "#e2e8f0";
const arpeggioMarkStrokeWidthInStaffSpaces = 0.12;
const arpeggioMarkWaveAmplitudeInStaffSpaces = 0.25;
const arpeggioMarkWaveSegmentHeightInStaffSpaces = 0.5;
const arpeggioMarkBracketTickWidthInStaffSpaces = 0.35;
const arpeggioMarkColor = "#e2e8f0";
const glissandoLineColor = "#f472b6";
const staffLineWidth = 1.5;
const spectrumReleaseClearMilliseconds = 120;
const scorePlaybackHighlightColor = "#a78bfa";
const scoreNoteSelectionColor = "#38bdf8";
const scoreNoteHitRadiusPixels = 12;
const plotLeftMargin = 44;
const plotRightMargin = 16;
const plotTopMargin = 26;
const plotBottomMargin = 34;

export function initialize(canvas, waveformCanvas, spectrumCanvas, analysisLayout) {
    initializeCanvas(canvas, waveformCanvas, spectrumCanvas, analysisLayout);
}

export function initializeScoreCanvas(canvas) {
    initializeCanvas(canvas);
}

function initializeCanvas(canvas, waveformCanvas, spectrumCanvas, analysisLayout) {
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
        selectedScoreNoteAddress: undefined,
        animationFrame: undefined,
        lastAudioActiveTimeMilliseconds: undefined,
        analyser: undefined,
        timeDomainData: undefined,
        frequencyData: undefined,
        analysisLayout,
        isWaveformVisible: false,
        isFrequencySpectrumVisible: false,
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

export function render(canvas, scene, isWaveformVisible, isFrequencySpectrumVisible, selectedScoreNoteAddress) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.scene = scene;
    state.selectedScoreNoteAddress = selectedScoreNoteAddress;
    state.isWaveformVisible = isWaveformVisible;
    state.isFrequencySpectrumVisible = isFrequencySpectrumVisible;
    state.scoreLayerDirty = true;
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

export function dispose(canvas) {
    const state = canvases.get(canvas);
    if (!state) {
        return;
    }

    state.resizeObserver.disconnect();
    if (state.animationFrame !== undefined) {
        cancelAnimationFrame(state.animationFrame);
    }

    canvases.delete(canvas);
}

function draw(state) {
    const { canvas, scene } = state;
    const surface = prepareCanvas(canvas);
    if (!surface) {
        return;
    }

    const { context, width, height, pixelRatio } = surface;

    if (scene.kind === pianoRollSceneKind) {
        drawPianoRoll(context, scene, width, height);
    } else {
        const scoreLayer = prepareScoreLayer(state, width, height, pixelRatio);
        context.drawImage(scoreLayer, 0, 0, width, height);
        drawScoreNoteSelection(
            context,
            scene,
            width,
            height,
            state.selectedScoreNoteAddress);
        const scorePlaybackBeats = getScorePlaybackBeats(state);
        drawScorePlaybackHighlights(context, scene, width, height, scorePlaybackBeats);
        drawLedgerLines(context, scene, width, height, getStaffSpace(scene, height));
        drawScoreCursor(context, state, width, height, scorePlaybackBeats);
    }

    if (state.isWaveformVisible) {
        drawOscilloscope(state);
    }
    if (state.isFrequencySpectrumVisible) {
        drawSpectrum(state);
    }
    ensureAnimation(state);
}

function drawScoreNoteSelection(context, scene, width, height, selectedAddress) {
    if (!isScoreNoteAddress(selectedAddress)) {
        return;
    }

    const selectedNote = scene.notes.find(note => scoreNoteAddressesEqual(note.address, selectedAddress));
    if (!selectedNote) {
        return;
    }

    const staffSpace = getStaffSpace(scene, height);
    const radiusX = (staffSpace * noteHeadWidthInStaffSpaces / 2) + 5;
    const radiusY = (staffSpace * noteHeadHeightInStaffSpaces / 2) + 5;
    context.strokeStyle = scoreNoteSelectionColor;
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
    drawGrandStaff(context, state.scene, width, height);

    state.scoreLayerDirty = false;
    state.scoreLayerWidth = width;
    state.scoreLayerHeight = height;
    state.scoreLayerPixelRatio = pixelRatio;
    return layer;
}

function drawGrandStaff(context, scene, width, height) {
    const staffSpace = getStaffSpace(scene, height);
    for (const band of scene.bands ?? []) {
        drawBand(context, band, width, height);
    }

    for (const line of scene.lines) {
        if (line.kind === ledgerLineKind) {
            continue;
        }

        drawLine(context, line, width, height, staffSpace);
    }

    let clefRight;
    for (const glyph of scene.glyphs) {
        if (glyph.kind !== clefGlyphKind) {
            continue;
        }

        const glyphRight = drawGlyph(context, glyph, width, height);
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
            drawGlyph(context, glyph, width, height);
        }
    }

    for (const tie of scene.ties ?? []) {
        drawTie(context, tie, width, height, staffSpace);
    }

    for (const slur of scene.slurs ?? []) {
        drawSlur(context, slur, width, height, staffSpace);
    }

    for (const arpeggioMark of scene.arpeggioMarks ?? []) {
        drawArpeggioMark(context, arpeggioMark, width, height, staffSpace);
    }

    for (const note of scene.notes) {
        if (!note.isActive) {
            drawNote(context, note, width, height, staffSpace);
        }
    }
    for (const beam of scene.beams ?? []) {
        drawBeam(context, beam, width, height, staffSpace);
    }
    for (const note of scene.notes) {
        if (note.isActive) {
            drawNote(context, note, width, height, staffSpace);
        }
    }
    drawLedgerLines(context, scene, width, height, staffSpace);

    if (shouldClipNoteElements) {
        context.restore();
    }
}

function drawLedgerLines(context, scene, width, height, staffSpace) {
    for (const line of scene.lines) {
        if (line.kind === ledgerLineKind) {
            drawLine(context, line, width, height, staffSpace);
        }
    }
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

function ensureAnimation(state) {
    const audioActive = isAudioActive();
    const now = performance.now();
    if (audioActive) {
        state.lastAudioActiveTimeMilliseconds = now;
    }

    const isAnalysisVisible = state.isWaveformVisible || state.isFrequencySpectrumVisible;
    const shouldAnimate = isScoreCursorActive(state)
        || (isAnalysisVisible
            && (audioActive
            || (state.lastAudioActiveTimeMilliseconds !== undefined
                && now - state.lastAudioActiveTimeMilliseconds < spectrumReleaseClearMilliseconds)));

    if (!shouldAnimate || state.animationFrame !== undefined) {
        return;
    }

    state.animationFrame = requestAnimationFrame(() => {
        state.animationFrame = undefined;
        draw(state);
    });
}

function isScoreCursorActive(state) {
    return Number.isFinite(getScorePlaybackBeats(state));
}

function drawPianoRoll(context, scene, width, height) {
    for (const bar of scene.bars) {
        const x0 = mapX(bar.rect.x0, width);
        const x1 = mapX(bar.rect.x1, width);
        const y0 = mapY(bar.rect.y0, height);
        const y1 = mapY(bar.rect.y1, height);
        context.fillStyle = bar.isActive ? "#22d3ee" : "#fbbf24";
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

function drawScorePlaybackHighlights(context, scene, width, height, scorePlaybackBeats) {
    if (!Number.isFinite(scorePlaybackBeats)) {
        return;
    }

    const staffSpace = getStaffSpace(scene, height);
    for (const note of scene.notes) {
        if (Number.isFinite(note.scoreOnsetBeats)
            && Number.isFinite(note.scoreEndBeats)
            && scorePlaybackBeats >= note.scoreOnsetBeats
            && scorePlaybackBeats < note.scoreEndBeats) {
            drawNote(context, note, width, height, staffSpace, scorePlaybackHighlightColor);
        }
    }
}

function drawScoreCursor(context, state, width, height, scorePlaybackBeats) {
    const cursor = state.scoreCursor;
    if (!cursor || !Number.isFinite(scorePlaybackBeats)) {
        return;
    }

    const visibleMeasureCount = cursor.visibleMeasureCount ?? defaultScoreCursorVisibleMeasureCount;
    const beats = Math.max(0, scorePlaybackBeats);
    const windowStartBeat = cursor.firstVisibleMeasure * cursor.beatsPerMeasure;
    const windowEndBeat = (cursor.firstVisibleMeasure + visibleMeasureCount)
        * cursor.beatsPerMeasure;
    if (beats < windowStartBeat || beats >= windowEndBeat) {
        return;
    }

    const x = mapScoreNotationBeatToX(beats, cursor.beatsPerMeasure, cursor.firstVisibleMeasure, visibleMeasureCount);
    drawLine(
        context,
        { x0: x, y0: cursor.cursorY0, x1: x, y1: cursor.cursorY1, kind: cursorLineKind },
        width,
        height);
}

// Mirrors GrandStaffLayout.MapAbsoluteBeatToScoreX / MapScoreOnsetToX.
export function mapAbsoluteBeatToScoreX(
    absoluteBeat,
    beatsPerMeasure,
    firstVisibleMeasure,
    visibleMeasureCount = defaultScoreCursorVisibleMeasureCount) {
    const measureIndex = Math.floor(absoluteBeat / beatsPerMeasure);
    const beatOffset = absoluteBeat - (measureIndex * beatsPerMeasure);
    const relativeMeasure = measureIndex - firstVisibleMeasure + (beatOffset / beatsPerMeasure);
    return scoreCursorX0 + (relativeMeasure / visibleMeasureCount) * (scoreCursorX1 - scoreCursorX0);
}

// Mirrors GrandStaffSceneBuilder.MapScoreNotationBeatToX for note and cursor alignment.
export function mapScoreNotationBeatToX(
    absoluteBeat,
    beatsPerMeasure,
    firstVisibleMeasure,
    visibleMeasureCount = defaultScoreCursorVisibleMeasureCount) {
    const measureIndex = Math.floor(absoluteBeat / beatsPerMeasure);
    const beatOffset = absoluteBeat - (measureIndex * beatsPerMeasure);
    const measureStartX = mapAbsoluteBeatToScoreX(
        measureIndex * beatsPerMeasure, beatsPerMeasure, firstVisibleMeasure, visibleMeasureCount);
    const measureEndX = mapAbsoluteBeatToScoreX(
        (measureIndex + 1) * beatsPerMeasure, beatsPerMeasure, firstVisibleMeasure, visibleMeasureCount);
    const noteAreaStartX = measureIndex === firstVisibleMeasure
        ? measureStartX
        : measureStartX + scoreNoteEdgeClearance;
    const noteAreaEndX = measureEndX - scoreNoteEdgeClearance;
    const x = noteAreaStartX + (beatOffset / beatsPerMeasure) * (noteAreaEndX - noteAreaStartX);
    return Math.min(Math.max(x, noteAreaStartX), noteAreaEndX);
}

function drawBand(context, band, width, height) {
    const x0 = mapX(band.x0, width);
    const x1 = mapX(band.x1, width);
    const y0 = mapY(band.y0, height);
    const y1 = mapY(band.y1, height);
    context.fillStyle = "rgba(51, 65, 85, 0.35)";
    context.beginPath();
    context.roundRect(x0, Math.min(y0, y1), x1 - x0, Math.abs(y1 - y0), 6);
    context.fill();
}

function drawLine(context, line, width, height, staffSpace) {
    context.strokeStyle = line.kind === cursorLineKind
        ? "#fb7185"
        : line.kind === beatLineKind
            ? "#334155"
            : line.kind === barlineKind
            ? "#64748b"
            : line.kind === staffLineKind
                ? "#94a3b8"
                : line.kind === glissandoLineKind ? glissandoLineColor : "#cbd5e1";
    context.lineWidth = line.kind === staffLineKind
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
    context.beginPath();
    context.moveTo(x0, mapY(line.y0, height));
    context.lineTo(x1, mapY(line.y1, height));
    context.stroke();
}

function drawGlyph(context, glyph, width, height) {
    if (glyph.kind === accidentalGlyphKind) {
        context.fillStyle = Number.isInteger(glyph.verdict)
            ? verdictColors[glyph.verdict]
            : glyph.isActive ? "#22d3ee" : "#fbbf24";
    } else {
        context.fillStyle = glyph.kind === clefGlyphKind ? "#e2e8f0" : "#f8fafc";
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

function drawNote(context, note, width, height, staffSpace, colorOverride) {
    const x = mapX(note.x, width);
    const y = mapY(note.y, height);
    const noteHeadRadiusX = staffSpace * noteHeadWidthInStaffSpaces / 2;
    const noteHeadRadiusY = staffSpace * noteHeadHeightInStaffSpaces / 2;
    const noteColor = colorOverride ?? (Number.isInteger(note.verdict)
        ? verdictColors[note.verdict]
        : note.isActive ? "#22d3ee" : "#fbbf24");
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
        stemX = x + (stemGoesUp ? noteHeadRadiusX : -noteHeadRadiusX);
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
        context.beginPath();
        context.arc(
            x + noteHeadRadiusX + (staffSpace * 0.25),
            y,
            Math.max(2, staffSpace * 0.08),
            0,
            Math.PI * 2);
        context.fill();
    }

    if (Number.isFinite(note.labelY)) {
        context.font = "16px system-ui, sans-serif";
        context.textAlign = "center";
        context.textBaseline = "top";
        context.fillText(note.label, x, mapY(note.labelY, height));
    }

    if (typeof note.fingering === "string" && Number.isFinite(note.fingeringY)) {
        context.fillStyle = "#f8fafc";
        context.font = "600 15px system-ui, sans-serif";
        context.textBaseline = "middle";
        context.fillText(note.fingering, x, mapY(note.fingeringY, height));
    }
}

function drawBeam(context, beam, width, height, staffSpace) {
    const stemGoesUp = beam.stemDirection === stemDirectionUp;
    const stemXOffset = staffSpace * noteHeadWidthInStaffSpaces / 2 * (stemGoesUp ? 1 : -1);
    const beamSpacingDirection = stemGoesUp ? 1 : -1;
    const x0 = mapX(beam.x0, width) + stemXOffset;
    const x1 = mapX(beam.x1, width) + stemXOffset;
    const y0 = mapY(beam.y0, height);
    const y1 = mapY(beam.y1, height);
    context.strokeStyle = "#fbbf24";
    context.lineWidth = Math.max(3, staffSpace * 0.45);
    context.lineCap = "butt";
    for (let beamIndex = 0; beamIndex < beam.count; beamIndex++) {
        const yOffset = beamIndex * staffSpace * 0.6 * beamSpacingDirection;
        context.beginPath();
        context.moveTo(x0, y0 + yOffset);
        context.lineTo(x1, y1 + yOffset);
        context.stroke();
    }
}

function drawTie(context, tie, width, height, staffSpace) {
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
        * staffSpace
        * tieCenterThicknessInStaffSpaces
        * 2 / 3;
    context.fillStyle = tie.isActive ? "#22d3ee" : "#fbbf24";
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
function drawSlur(context, slur, width, height, staffSpace) {
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
    context.strokeStyle = slurColor;
    context.lineWidth = Math.max(1, staffSpace * slurStrokeWidthInStaffSpaces);
    context.beginPath();
    context.moveTo(x0, y0);
    context.bezierCurveTo(controlX1, controlY1, controlX2, controlY2, x1, y1);
    context.stroke();
}

// A vertical arpeggio mark to the left of a chord: a wavy line (quadratic-curve zigzag) for
// GrandStaffArpeggioMark.IsNonArpeggiate false, or a straight bracket for true — no existing
// primitive to adapt (ties/beams/slurs are all horizontal-ish; this is the first vertical mark).
function drawArpeggioMark(context, mark, width, height, staffSpace) {
    const x = mapX(mark.x, width);
    const yTop = mapY(Math.max(mark.y0, mark.y1), height);
    const yBottom = mapY(Math.min(mark.y0, mark.y1), height);
    context.strokeStyle = arpeggioMarkColor;
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
        || Math.abs(x - scoreCursorX1) < Number.EPSILON;
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
