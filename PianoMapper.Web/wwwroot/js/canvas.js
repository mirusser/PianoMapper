import { getAnalyserNode, isAudioActive } from "./audio.js";

const canvases = new Map();
const staffLineKind = 0;
const ledgerLineKind = 1;
const defaultStaffSpace = 11;
const noteHeadWidthInStaffSpaces = 1.4;
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
        scene: { kind: 0, lines: [], glyphs: [], notes: [] },
        animationFrame: undefined,
        lastAudioActiveTimeMilliseconds: undefined,
        analyser: undefined,
        timeDomainData: undefined,
        frequencyData: undefined,
        analysisLayout,
    };
    state.resizeObserver = new ResizeObserver(() => draw(state));
    state.resizeObserver.observe(canvas);
    state.resizeObserver.observe(waveformCanvas);
    state.resizeObserver.observe(spectrumCanvas);
    canvases.set(canvas, state);
    draw(state);
}

export function render(canvas, scene) {
    const state = canvases.get(canvas);
    if (!state) {
        throw new Error("Canvas is not initialized.");
    }

    state.scene = scene;
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

    const { context, width, height } = surface;

    if (scene.kind === 1) {
        drawPianoRoll(context, scene, width, height);
    } else {
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
            drawNote(context, note, width, height, staffSpace);
        }

        if (shouldClipNoteElements) {
            context.restore();
        }
    }

    drawOscilloscope(state);
    drawSpectrum(state);
    ensureAnimation(state);
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
    return { context, width: bounds.width, height: bounds.height };
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

    const analyser = getAnalyserNode();
    if (!analyser) {
        return;
    }

    if (state.analyser !== analyser) {
        state.analyser = analyser;
        state.timeDomainData = new Uint8Array(analyser.fftSize);
        state.frequencyData = new Uint8Array(analyser.frequencyBinCount);
    }

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

    if (!state.analyser || !state.frequencyData) {
        return;
    }

    if (isAudioActive()) {
        state.analyser.getByteFrequencyData(state.frequencyData);
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

function ensureAnimation(state) {
    const audioActive = isAudioActive();
    const now = performance.now();
    if (audioActive) {
        state.lastAudioActiveTimeMilliseconds = now;
    }

    const shouldAnimate = audioActive
        || (state.lastAudioActiveTimeMilliseconds !== undefined
            && now - state.lastAudioActiveTimeMilliseconds < spectrumReleaseClearMilliseconds);

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

function drawLine(context, line, width, height) {
    context.strokeStyle = line.kind === 3
        ? "#fb7185"
        : line.kind === 2
            ? "#64748b"
            : line.kind === 0 ? "#94a3b8" : "#cbd5e1";
    context.lineWidth = line.kind === staffLineKind ? staffLineWidth : line.kind === 3 ? 2 : 1.5;
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
    const noteHeadRadiusY = staffSpace / 2;
    const noteColor = Number.isInteger(note.verdict)
        ? verdictColors[note.verdict]
        : note.isActive ? "#22d3ee" : "#fbbf24";
    context.strokeStyle = noteColor;
    context.fillStyle = context.strokeStyle;
    context.lineWidth = 2;
    let labelX = x + noteHeadRadiusX + 4;
    if (note.isActive && note.durationSeconds > 0) {
        const durationWidth = Math.min(80, Math.max(12, note.durationSeconds * 40));
        context.beginPath();
        context.moveTo(x, y);
        context.lineTo(x + durationWidth, y);
        context.stroke();
        labelX = x + durationWidth + 4;
    }

    context.beginPath();
    context.ellipse(x, y, noteHeadRadiusX, noteHeadRadiusY, -0.2, 0, Math.PI * 2);
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
        stemEndY = y + (stemGoesUp ? -1 : 1) * staffSpace * stemLengthInStaffSpaces;
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

    context.font = "12px system-ui, sans-serif";
    context.textAlign = "left";
    context.textBaseline = "alphabetic";
    context.fillText(note.label, labelX, y + 4);
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
