import { getAnalyserNode, isAudioActive } from "./audio.js";

const canvases = new Map();
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
            drawLine(context, line, width, height);
        }

        for (const glyph of scene.glyphs) {
            drawGlyph(context, glyph, width, height);
        }

        for (const note of scene.notes) {
            drawNote(context, note, width, height);
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

function drawOscilloscope(state) {
    const surface = prepareCanvas(state.waveformCanvas);
    if (!surface) {
        return;
    }

    const { context, width, height } = surface;
    const x0 = 12;
    const x1 = width - 12;
    const y0 = height - 12;
    const y1 = 12;
    const centerY = (y0 + y1) / 2;
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
    if (!state.analyser || !state.frequencyData) {
        return;
    }

    state.analyser.getByteFrequencyData(state.frequencyData);
    const layout = state.analysisLayout;
    const visibleCount = Math.min(layout.spectrumVisibleBinCount, state.frequencyData.length);
    let maximum = 0;
    for (let index = 0; index < visibleCount; index++) {
        maximum = Math.max(maximum, state.frequencyData[index]);
    }

    const panelX0 = 12;
    const panelX1 = width - 12;
    const panelY0 = height - 12;
    const panelY1 = 12;
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
    if (!isAudioActive() || state.animationFrame !== undefined) {
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
    context.lineWidth = line.kind === 0 ? 1 : line.kind === 3 ? 2 : 1.5;
    context.beginPath();
    context.moveTo(mapX(line.x0, width), mapY(line.y0, height));
    context.lineTo(mapX(line.x1, width), mapY(line.y1, height));
    context.stroke();
}

function drawGlyph(context, glyph, width, height) {
    context.fillStyle = glyph.kind === 0 ? "#e2e8f0" : "#f8fafc";
    context.font = glyph.kind === 0
        ? "34px 'Noto Music', 'Bravura Text', serif"
        : "18px 'Noto Music', 'Bravura Text', serif";
    context.textAlign = "center";
    context.textBaseline = "middle";
    context.fillText(glyph.text, mapX(glyph.x, width), mapY(glyph.y, height));
}

function drawNote(context, note, width, height) {
    const x = mapX(note.x, width);
    const y = mapY(note.y, height);
    const noteColor = Number.isInteger(note.verdict)
        ? verdictColors[note.verdict]
        : note.isActive ? "#22d3ee" : "#fbbf24";
    context.strokeStyle = noteColor;
    context.fillStyle = context.strokeStyle;
    context.lineWidth = 2;
    if (note.durationSeconds > 0) {
        const durationWidth = Math.min(80, Math.max(12, note.durationSeconds * 40));
        context.beginPath();
        context.moveTo(x - durationWidth, y);
        context.lineTo(x, y);
        context.stroke();
    }

    context.beginPath();
    context.ellipse(x, y, 8, 5.5, -0.2, 0, Math.PI * 2);
    if (note.isFilled) {
        context.fill();
    } else {
        context.stroke();
    }

    let stemEndY = y;
    let stemX = x;
    if (note.hasStem) {
        const stemGoesUp = note.stemDirection === 0;
        stemX = x + (stemGoesUp ? 7 : -7);
        stemEndY = y + (stemGoesUp ? -30 : 30);
        context.beginPath();
        context.moveTo(stemX, y);
        context.lineTo(stemX, stemEndY);
        context.stroke();
    }

    if (note.needsFlag) {
        const flagDirection = note.stemDirection === 0 ? 1 : -1;
        context.beginPath();
        context.moveTo(stemX, stemEndY);
        context.quadraticCurveTo(stemX + (10 * flagDirection), stemEndY + 8, stemX, stemEndY + 15);
        context.stroke();
    }

    if (note.hasDot) {
        context.beginPath();
        context.arc(x + 14, y, 2, 0, Math.PI * 2);
        context.fill();
    }

    context.font = "12px system-ui, sans-serif";
    context.textAlign = "left";
    context.textBaseline = "alphabetic";
    context.fillText(note.label, x + 11, y + 4);
}

function mapX(value, width) {
    const padding = 18;
    return padding + ((value + 1) / 2) * Math.max(0, width - (padding * 2));
}

function mapY(value, height) {
    const padding = 18;
    return height - padding - ((value + 1) / 2) * Math.max(0, height - (padding * 2));
}
