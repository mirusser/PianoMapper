import { getAnalyserNode, getCurrentTime, isAudioActive } from "./audio.js";

const canvases = new Map();
const staffLineKind = 0;
const ledgerLineKind = 1;
const barlineKind = 2;
const cursorLineKind = 3;
const beatLineKind = 4;
// Mirrors PianoMapper.Core/Rendering/GrandStaffLayout.cs's ScoreX0/ScoreX1/VisibleMeasureCount
// constants, so the score-playback cursor can be positioned here every animation frame from the
// Web Audio clock, instead of C# rebuilding the whole grand-staff scene every tick just to move
// the cursor line.
const scoreCursorX0 = -0.56;
const scoreCursorX1 = 0.96;
const scoreCursorVisibleMeasureCount = 6;
const defaultStaffSpace = 11;
const noteHeadWidthInStaffSpaces = 1.2;
const noteHeadHeightInStaffSpaces = 0.8;
const noteHeadRotationRadians = -Math.PI / 8;
const stemLengthInStaffSpaces = 3;
const flagControlWidthInStaffSpaces = 1.2;
const flagControlHeightInStaffSpaces = 0.5;
const flagHeightInStaffSpaces = 1.15;
const flagSpacingInStaffSpaces = 0.45;
const staffLineWidth = 1.5;
const spectrumReleaseClearMilliseconds = 120;
const plotLeftMargin = 44;
const plotRightMargin = 16;
const plotTopMargin = 26;
const plotBottomMargin = 34;
const verdictColors = [
    "#4ade80",
    "#f87171",
    "#fb923c",
    "#fb923c",
    "#facc15",
    "#facc15",
    "#94a3b8",
    "#c084fc",
];

export function initialize(canvas, waveformCanvas, spectrumCanvas, analysisLayout) {
    dispose(canvas);

    const state = {
        canvas,
        waveformCanvas,
        spectrumCanvas,
        scene: { kind: 0, lines: [], glyphs: [], notes: [], beams: [] },
        scoreLayerCanvas: document.createElement("canvas"),
        scoreLayerDirty: true,
        scoreLayerWidth: undefined,
        scoreLayerHeight: undefined,
        scoreLayerPixelRatio: undefined,
        scoreCursor: undefined,
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
    state.resizeObserver.observe(waveformCanvas);
    state.resizeObserver.observe(spectrumCanvas);
    canvases.set(canvas, state);
    draw(state);
}

export function render(canvas, scene, isWaveformVisible, isFrequencySpectrumVisible) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.scene = scene;
    state.isWaveformVisible = isWaveformVisible;
    state.isFrequencySpectrumVisible = isFrequencySpectrumVisible;
    state.scoreLayerDirty = true;
    draw(state);
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
}

export function stopScoreCursor(canvas) {
    const state = canvases.get(canvas);
    if (!state) {
        return;
    }

    state.scoreCursor = undefined;
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

    if (scene.kind === 1) {
        drawPianoRoll(context, scene, width, height);
    } else {
        const scoreLayer = prepareScoreLayer(state, width, height, pixelRatio);
        context.drawImage(scoreLayer, 0, 0, width, height);
        drawScoreCursor(context, state, width, height);
    }

    if (state.isWaveformVisible) {
        drawOscilloscope(state);
    }
    if (state.isFrequencySpectrumVisible) {
        drawSpectrum(state);
    }
    ensureAnimation(state);
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
    for (const line of scene.lines) {
        if (scene.shouldClipNotesAtClefs && line.kind === ledgerLineKind) {
            continue;
        }

        drawLine(context, line, width, height);
    }

    let clefRight;
    for (const glyph of scene.glyphs) {
        if (glyph.kind !== 0) {
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
        const noteAreaX0 = clefRight + clefGap;
        context.save();
        context.beginPath();
        context.rect(noteAreaX0, 0, Math.max(0, width - noteAreaX0), height);
        context.clip();
    }

    if (scene.shouldClipNotesAtClefs) {
        for (const line of scene.lines) {
            if (line.kind === ledgerLineKind) {
                drawLine(context, line, width, height);
            }
        }
    }

    for (const glyph of scene.glyphs) {
        if (glyph.kind !== 0) {
            drawGlyph(context, glyph, width, height);
        }
    }

    const staffLines = scene.lines.filter(line => line.kind === staffLineKind);
    const staffSpace = staffLines.length >= 2
        ? mapHeight(Math.abs(staffLines[1].y0 - staffLines[0].y0), height)
        : defaultStaffSpace;
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

    if (shouldClipNoteElements) {
        context.restore();
    }
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
    const shouldAnimate = isAnalysisVisible
        && (audioActive
            || (state.lastAudioActiveTimeMilliseconds !== undefined
                && now - state.lastAudioActiveTimeMilliseconds < spectrumReleaseClearMilliseconds));

    if (!shouldAnimate || state.animationFrame !== undefined) {
        return;
    }

    state.animationFrame = requestAnimationFrame(() => {
        state.animationFrame = undefined;
        draw(state);
    });
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

function drawScoreCursor(context, state, width, height) {
    const cursor = state.scoreCursor;
    if (!cursor) {
        return;
    }

    let currentTime;
    try {
        currentTime = getCurrentTime();
    } catch {
        // Audio isn't initialized (yet, or anymore). Nothing to animate from.
        return;
    }

    if (currentTime > cursor.completionSeconds) {
        return;
    }

    const beats = Math.max(0, (currentTime - cursor.anchorSeconds) / 60 * cursor.beatsPerMinute);
    const x = mapAbsoluteBeatToScoreX(beats, cursor.beatsPerMeasure, cursor.firstVisibleMeasure);
    if (x < scoreCursorX0 || x > scoreCursorX1) {
        return;
    }

    drawLine(
        context,
        { x0: x, y0: cursor.cursorY0, x1: x, y1: cursor.cursorY1, kind: cursorLineKind },
        width,
        height);
}

// Mirrors GrandStaffLayout.MapAbsoluteBeatToScoreX / MapScoreOnsetToX.
function mapAbsoluteBeatToScoreX(absoluteBeat, beatsPerMeasure, firstVisibleMeasure) {
    const measureIndex = Math.floor(absoluteBeat / beatsPerMeasure);
    const beatOffset = absoluteBeat - (measureIndex * beatsPerMeasure);
    const relativeMeasure = measureIndex - firstVisibleMeasure + (beatOffset / beatsPerMeasure);
    return scoreCursorX0 + (relativeMeasure / scoreCursorVisibleMeasureCount) * (scoreCursorX1 - scoreCursorX0);
}

function drawLine(context, line, width, height) {
    context.strokeStyle = line.kind === cursorLineKind
        ? "#fb7185"
        : line.kind === beatLineKind
            ? "#334155"
            : line.kind === barlineKind
            ? "#64748b"
            : line.kind === 0 ? "#94a3b8" : "#cbd5e1";
    context.lineWidth = line.kind === staffLineKind
        ? staffLineWidth
        : line.kind === cursorLineKind
            ? 2
            : line.kind === beatLineKind ? 1 : 1.5;
    context.beginPath();
    context.moveTo(mapX(line.x0, width), mapY(line.y0, height));
    context.lineTo(mapX(line.x1, width), mapY(line.y1, height));
    context.stroke();
}

function drawGlyph(context, glyph, width, height) {
    context.fillStyle = glyph.kind === 0 ? "#e2e8f0" : "#f8fafc";
    context.textAlign = "center";
    const x = mapX(glyph.x, width);
    const y = mapY(glyph.y, height);
    if (glyph.kind === 0 && glyph.height > 0) {
        const measurementFontSize = 100;
        context.font = `${measurementFontSize}px 'Noto Music', 'Bravura Text', serif`;
        const measurement = context.measureText(glyph.text);
        const measuredHeight = measurement.actualBoundingBoxAscent + measurement.actualBoundingBoxDescent;
        const targetHeight = mapHeight(glyph.height, height);
        const fontSize = measurementFontSize * targetHeight / measuredHeight;
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

function drawNote(context, note, width, height, staffSpace) {
    const x = mapX(note.x, width);
    const y = mapY(note.y, height);
    const noteHeadRadiusX = staffSpace * noteHeadWidthInStaffSpaces / 2;
    const noteHeadRadiusY = staffSpace * noteHeadHeightInStaffSpaces / 2;
    const noteColor = Number.isInteger(note.verdict)
        ? verdictColors[note.verdict]
        : note.isActive ? "#22d3ee" : "#fbbf24";
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
    const stemGoesUp = note.stemDirection === 0;
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

    context.font = "16px system-ui, sans-serif";
    context.textAlign = "center";
    context.textBaseline = "top";
    context.fillText(note.label, x, y + noteHeadRadiusY + 6);
}

function drawBeam(context, beam, width, height, staffSpace) {
    const stemGoesUp = beam.stemDirection === 0;
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
