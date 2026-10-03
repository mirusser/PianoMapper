import assert from "node:assert/strict";
import test from "node:test";

import {
    dispose,
    glissandoLineKind,
    hitTestScoreNote,
    initialize,
    initializeScoreCanvas,
    mapAbsoluteBeatToScoreX,
    mapScoreNotationBeatToX,
    octaveShiftLineKind,
    render,
    startScoreCursor,
    stopScoreCursor,
    verdictColors,
} from "../../PianoMapper.Web/wwwroot/js/canvas.js";
import {
    dispose as disposeAudio,
    initialize as initializeAudio,
} from "../../PianoMapper.Web/wwwroot/js/audio.js";

test("score cursor mapping fits five measures across the score width", () => {
    const fifthMeasureBoundary = mapAbsoluteBeatToScoreX(20, 4, 0);

    assert.ok(Math.abs(fifthMeasureBoundary - 0.96) < 1e-9);
});

test("score notation cursor keeps consecutive eighth-note onsets evenly spaced", () => {
    const onsets = [6, 6.5, 7].map(beat => mapScoreNotationBeatToX(beat, 3, 0));

    assert.ok(onsets[0] > mapAbsoluteBeatToScoreX(6, 3, 0));
    assert.ok(Math.abs((onsets[1] - onsets[0]) - (onsets[2] - onsets[1])) < 1e-9);
});

class FakeCanvasContext {
    strokeCalls = 0;
    drawImageCalls = 0;
    ellipseCalls = [];
    arcCalls = [];
    curveCalls = [];
    filledPathCalls = [];
    strokedPathCalls = [];
    fillTextCalls = [];
    lineSegments = [];
    measureTextCalls = 0;
    clipCalls = 0;
    operations = [];
    rectCalls = [];
    roundRectCalls = [];
    lineDashCalls = [];
    pathStart = undefined;
    currentPath = undefined;
    // Real Canvas2D measureText returns different bounding boxes per character/font — a "flat"
    // glyph (a dash, centered near the baseline) measures a much smaller actualBoundingBoxAscent+
    // Descent than a letter-like one. Default to the previous fixed stub (a normal, letter-like
    // box) for every text, and let a test override specific strings via metricsByText to
    // reproduce that real-world variation (see the "flat glyph" regression test below).
    metricsByText = {};

    setTransform() { }
    clearRect() { }
    fillRect() { }
    strokeRect() { }
    setLineDash(pattern) {
        this.lineDashCalls.push([...pattern]);
    }
    fillText(...args) {
        this.fillTextCalls.push({ args, font: this.font, fillStyle: this.fillStyle });
    }
    measureText(text) {
        this.measureTextCalls++;
        return this.metricsByText[text] ?? {
            actualBoundingBoxAscent: 80,
            actualBoundingBoxDescent: 20,
            actualBoundingBoxRight: 50,
            // A proportional text model (half the font size per character) so label-fitting is deterministic here.
            width: (Number.parseFloat(this.font) || 16) * text.length * 0.5,
        };
    }
    save() { }
    translate() { }
    rotate() { }
    restore() { }
    beginPath() {
        this.pathStart = undefined;
        this.lastRoundRect = undefined;
        this.currentPath = { bezierCurves: [], isClosed: false };
    }
    moveTo(x, y) {
        this.pathStart = { x, y };
        if (this.currentPath) {
            this.currentPath.start = { x, y };
        }
    }
    lineTo(x, y) {
        if (this.pathStart) {
            const segment = { ...this.pathStart, x1: x, y1: y };
            this.lineSegments.push(segment);
            this.operations.push({ kind: "line", segment });
        }
    }
    rect(...args) {
        this.rectCalls.push(args);
    }
    roundRect(x, y, width, height, radius) {
        const call = {
            x,
            y,
            width,
            height,
            radius,
            strokeStyle: this.strokeStyle,
            lineWidth: this.lineWidth,
            lineDash: [...(this.lineDashCalls.at(-1) ?? [])],
            stroked: false,
        };
        this.lastRoundRect = call;
        this.roundRectCalls.push(call);
        this.operations.push({ kind: "roundRect", call });
    }
    clip() {
        this.clipCalls++;
    }
    arc(...args) {
        this.arcCalls.push(args);
    }
    quadraticCurveTo(controlX, controlY, x, y) {
        const curve = {
            ...this.pathStart,
            controlX,
            controlY,
            x1: x,
            y1: y,
            strokeStyle: this.strokeStyle,
            lineWidth: this.lineWidth,
            clipCalls: this.clipCalls,
        };
        this.curveCalls.push(curve);
        this.operations.push({ kind: "curve", curve });
        this.pathStart = { x, y };
    }
    bezierCurveTo(controlX1, controlY1, controlX2, controlY2, x, y) {
        const curve = {
            ...this.pathStart,
            controlX1,
            controlY1,
            controlX2,
            controlY2,
            x1: x,
            y1: y,
        };
        this.currentPath?.bezierCurves.push(curve);
        this.pathStart = { x, y };
    }
    closePath() {
        if (this.currentPath) {
            this.currentPath.isClosed = true;
        }
    }
    fill() {
        if (this.currentPath?.bezierCurves.length > 0) {
            const filledPath = {
                ...this.currentPath,
                fillStyle: this.fillStyle,
                clipCalls: this.clipCalls,
            };
            this.filledPathCalls.push(filledPath);
            this.operations.push({ kind: "filledPath", filledPath });
        }
    }

    ellipse(...args) {
        this.ellipseCalls.push(args);
        this.operations.push({ kind: "ellipse", args });
    }

    stroke() {
        this.strokeCalls++;
        if (this.lastRoundRect) {
            this.lastRoundRect.stroked = true;
        }

        if (this.currentPath?.bezierCurves.length > 0) {
            const strokedPath = {
                ...this.currentPath,
                strokeStyle: this.strokeStyle,
                lineWidth: this.lineWidth,
            };
            this.strokedPathCalls.push(strokedPath);
            this.operations.push({ kind: "strokedPath", strokedPath });
        }
    }

    drawImage() {
        this.drawImageCalls++;
    }
}

class FakeCanvas {
    width = 0;
    height = 0;
    clientWidth = 640;
    clientHeight = 240;
    context = new FakeCanvasContext();

    getBoundingClientRect() {
        return { width: this.clientWidth, height: this.clientHeight };
    }

    getContext() {
        return this.context;
    }
}

async function createScoreCursorHarness(currentTime) {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const animationFrames = [];
    const cancelledFrames = [];
    let nextAnimationFrame = 1;

    class FakeAudioContext {
        state = "running";
        destination = {};
        currentTime = currentTime;

        constructor() {
            globalThis.window.AudioContextInstance = this;
        }

        createGain() {
            return { gain: { value: 0 }, connect() { } };
        }

        createAnalyser() {
            return { connect() { } };
        }

        async close() {
            this.state = "closed";
        }
    }

    globalThis.window = { devicePixelRatio: 1, AudioContext: FakeAudioContext };
    globalThis.document = {
        cookie: "pianomapper-sound-source=synth",
        createElement() {
            return new FakeCanvas();
        },
        querySelector() {
            return null;
        },
        querySelectorAll() {
            return [];
        },
    };
    globalThis.ResizeObserver = class {
        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = callback => {
        const id = nextAnimationFrame++;
        animationFrames.push({ id, callback });
        return id;
    };
    globalThis.cancelAnimationFrame = id => cancelledFrames.push(id);

    await initializeAudio();
    const audioContext = window.AudioContextInstance;
    const canvases = [];

    return {
        animationFrames,
        cancelledFrames,
        audioContext,
        createCanvas() {
            const canvas = new FakeCanvas();
            initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
            canvases.push(canvas);
            return canvas;
        },
        async dispose() {
            for (const canvas of canvases) {
                dispose(canvas);
            }
            await disposeAudio();
            globalThis.window = originalWindow;
            globalThis.document = originalDocument;
            globalThis.ResizeObserver = originalResizeObserver;
            globalThis.requestAnimationFrame = originalRequestAnimationFrame;
            globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
        },
    };
}

function createEditableScoreScene(notes) {
    return {
        kind: 0,
        lines: [
            { x0: -1, y0: 0.5, x1: 1, y1: 0.5, kind: 0 },
            { x0: -1, y0: 0.4, x1: 1, y1: 0.4, kind: 0 },
        ],
        glyphs: [],
        notes,
        beams: [],
        ties: [],
        bands: [],
    };
}

test("score note hit testing selects the closest chord member", async () => {
    const harness = await createScoreCursorHarness(0);
    const canvas = harness.createCanvas();
    const scene = createEditableScoreScene([
        { x: 0, y: 0.2, address: { measureIndex: 0, noteIndex: 0 } },
        { x: 0, y: -0.2, address: { measureIndex: 0, noteIndex: 1 } },
    ]);

    try {
        render(canvas, scene, false, false);

        assert.deepEqual(hitTestScoreNote(canvas, 320, 96), { measureIndex: 0, noteIndex: 0 });
        assert.deepEqual(hitTestScoreNote(canvas, 320, 144), { measureIndex: 0, noteIndex: 1 });
    } finally {
        await harness.dispose();
    }
});

test("score note hit testing returns null away from noteheads", async () => {
    const harness = await createScoreCursorHarness(0);
    const canvas = harness.createCanvas();

    try {
        render(
            canvas,
            createEditableScoreScene([
                { x: 0, y: 0, address: { measureIndex: 2, noteIndex: 3 } },
            ]),
            false,
            false);

        assert.equal(hitTestScoreNote(canvas, 40, 40), null);
    } finally {
        await harness.dispose();
    }
});

test("score note hit testing follows responsive canvas size", async () => {
    const harness = await createScoreCursorHarness(0);
    const canvas = harness.createCanvas();
    canvas.clientWidth = 320;
    canvas.clientHeight = 120;

    try {
        render(
            canvas,
            createEditableScoreScene([
                { x: 0.5, y: -0.5, address: { measureIndex: 4, noteIndex: 2 } },
            ]),
            false,
            false);

        assert.deepEqual(hitTestScoreNote(canvas, 231, 81), { measureIndex: 4, noteIndex: 2 });
    } finally {
        await harness.dispose();
    }
});

test("selected score note draws an overlay ring", async () => {
    const harness = await createScoreCursorHarness(0);
    const canvas = harness.createCanvas();
    const address = { measureIndex: 1, noteIndex: 2 };

    try {
        render(
            canvas,
            createEditableScoreScene([
                { x: 0, y: 0, address },
            ]),
            false,
            false,
            address);

        assert.equal(canvas.context.ellipseCalls.length, 1);
    } finally {
        await harness.dispose();
    }
});

test("score cursor exact boundary belongs only to incoming canvas", async () => {
    const harness = await createScoreCursorHarness(20);
    const outgoingCanvas = harness.createCanvas();
    const incomingCanvas = harness.createCanvas();
    const cursor = {
        anchorSeconds: 0,
        beatsPerMinute: 60,
        beatsPerMeasure: 4,
        completionSeconds: 40,
        cursorY0: -0.5,
        cursorY1: 0.5,
        visibleMeasureCount: 5,
    };

    try {
        startScoreCursor(outgoingCanvas, { ...cursor, firstVisibleMeasure: 0 });
        startScoreCursor(incomingCanvas, { ...cursor, firstVisibleMeasure: 5 });

        assert.equal(outgoingCanvas.context.lineSegments.length, 0);
        assert.equal(incomingCanvas.context.lineSegments.length, 1);
    } finally {
        await harness.dispose();
    }
});

test("score cursor follows an implicit pickup into its following page", async () => {
    const harness = await createScoreCursorHarness(17);
    const canvas = harness.createCanvas();
    const cursor = {
        anchorSeconds: 0,
        beatsPerMinute: 60,
        beatsPerMeasure: 4,
        firstVisibleMeasure: 5,
        completionSeconds: 40,
        cursorY0: -0.5,
        cursorY1: 0.5,
        visibleMeasureCount: 5,
        measureStartBeats: [0, 1, 5, 9, 13, 17, 21, 25, 29, 33, 37],
    };

    try {
        startScoreCursor(canvas, cursor);

        assert.equal(canvas.context.lineSegments.length, 1);
    } finally {
        await harness.dispose();
    }
});

test("score cursor X reflects a non-default visibleMeasureCount", async () => {
    const harness = await createScoreCursorHarness(2);
    const defaultCanvas = harness.createCanvas();
    const narrowedCanvas = harness.createCanvas();
    const cursor = {
        anchorSeconds: 0,
        beatsPerMinute: 60,
        beatsPerMeasure: 4,
        firstVisibleMeasure: 0,
        completionSeconds: 10,
        cursorY0: -0.5,
        cursorY1: 0.5,
    };

    try {
        startScoreCursor(defaultCanvas, { ...cursor, visibleMeasureCount: 5 });
        startScoreCursor(narrowedCanvas, { ...cursor, visibleMeasureCount: 2 });

        const defaultX = defaultCanvas.context.lineSegments.at(-1).x;
        const narrowedX = narrowedCanvas.context.lineSegments.at(-1).x;
        assert.notEqual(narrowedX, defaultX);
    } finally {
        await harness.dispose();
    }
});

test("score cursor animates without analysis panels and stops at completion or removal", async () => {
    const harness = await createScoreCursorHarness(1);
    const canvas = harness.createCanvas();
    const cursor = {
        anchorSeconds: 0,
        beatsPerMinute: 60,
        beatsPerMeasure: 4,
        firstVisibleMeasure: 0,
        completionSeconds: 2,
        cursorY0: -0.5,
        cursorY1: 0.5,
        visibleMeasureCount: 5,
    };

    try {
        startScoreCursor(canvas, cursor);
        assert.equal(harness.animationFrames.length, 1);

        harness.audioContext.currentTime = 3;
        harness.animationFrames.shift().callback();
        assert.equal(harness.animationFrames.length, 0);

        harness.audioContext.currentTime = 1;
        startScoreCursor(canvas, cursor);
        assert.equal(harness.animationFrames.length, 1);
        const scheduledFrame = harness.animationFrames[0].id;

        stopScoreCursor(canvas);

        assert.deepEqual(harness.cancelledFrames, [scheduledFrame]);
    } finally {
        await harness.dispose();
    }
});

test("score playback highlights a chord only inside its half-open beat interval", async () => {
    const harness = await createScoreCursorHarness(1);
    const canvas = harness.createCanvas();
    const noteX = mapScoreNotationBeatToX(1, 4, 0);
    const note = {
        y: 0.2,
        scoreOnsetBeats: 1,
        scoreEndBeats: 2,
        isActive: false,
        isFilled: true,
        label: "C4",
    };

    try {
        render(canvas, {
            kind: 0,
            lines: [
                { x0: -0.8, y0: 0.3, x1: 0.8, y1: 0.3, kind: 0 },
                { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            ],
            glyphs: [],
            notes: [
                { ...note, x: noteX },
                { ...note, x: noteX, y: 0.1, label: "E4" },
            ],
            beams: [],
            shouldClipNotesAtClefs: false,
        });
        startScoreCursor(canvas, {
            anchorSeconds: 0,
            beatsPerMinute: 60,
            beatsPerMeasure: 4,
            firstVisibleMeasure: 0,
            completionSeconds: 5,
            cursorY0: -0.5,
            cursorY1: 0.5,
            visibleMeasureCount: 5,
        });

        assert.equal(canvas.context.ellipseCalls.length, 2);
        assert.equal(canvas.context.fillStyle, "#a78bfa");
        assert.equal(canvas.context.ellipseCalls[0][0], canvas.context.lineSegments.at(-1).x);

        canvas.context.ellipseCalls.length = 0;
        harness.audioContext.currentTime = 2;
        harness.animationFrames.shift().callback();

        assert.equal(canvas.context.ellipseCalls.length, 0);
    } finally {
        await harness.dispose();
    }
});

function withCanvasMocks(callback, { width = 640, height = 240, onCreateElement } = {}) {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];
    const resizeCallbacks = [];
    const observers = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement(tagName) {
            onCreateElement?.(tagName);
            const createdCanvas = new FakeCanvas();
            createdCanvas.clientWidth = width;
            createdCanvas.clientHeight = height;
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observed = [];
        isDisconnected = false;

        constructor(resizeCallback) {
            resizeCallbacks.push(resizeCallback);
            observers.push(this);
        }

        observe(element) {
            this.observed.push(element);
        }

        disconnect() {
            this.isDisconnected = true;
        }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

    try {
        return callback({ createdCanvases, resizeCallbacks, observers });
    } finally {
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
}

function renderGrandStaffScene(scene, width = 640, height = 240, metricsByText = undefined) {
    return withCanvasMocks(({ createdCanvases }) => {
        const canvas = new FakeCanvas();
        canvas.clientWidth = width;
        canvas.clientHeight = height;
        try {
            initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
            if (metricsByText) {
                createdCanvases[0].context.metricsByText = metricsByText;
            }
            render(canvas, scene);
            return createdCanvases[0].context;
        } finally {
            dispose(canvas);
        }
    }, { width, height });
}

test("grand staff drawing caches its static layer until the scene or size changes", () => {
    withCanvasMocks(({ createdCanvases, resizeCallbacks }) => {
        const canvas = new FakeCanvas();
        const waveformCanvas = new FakeCanvas();
        const spectrumCanvas = new FakeCanvas();

        try {
            initialize(canvas, waveformCanvas, spectrumCanvas, { spectrumVisibleBinCount: 32 });
            render(canvas, {
                kind: 0,
                lines: [{ x0: -0.8, y0: 0.5, x1: 0.8, y1: 0.5, kind: 0 }],
                glyphs: [],
                notes: [],
                shouldClipNotesAtClefs: false,
            });

            const strokesAfterSceneRender = canvas.context.strokeCalls;
            const blitsAfterSceneRender = canvas.context.drawImageCalls;
            const scoreLayer = createdCanvases[0];
            const scoreLayerStrokesAfterSceneRender = scoreLayer.context.strokeCalls;

            resizeCallbacks[0]();

            assert.equal(canvas.context.strokeCalls, strokesAfterSceneRender);
            assert.equal(canvas.context.drawImageCalls, blitsAfterSceneRender + 1);
            assert.equal(scoreLayer.context.strokeCalls, scoreLayerStrokesAfterSceneRender);

            canvas.clientWidth = 800;
            resizeCallbacks[0]();

            assert.equal(scoreLayer.context.strokeCalls, scoreLayerStrokesAfterSceneRender + 1);

            render(canvas, {
                kind: 0,
                lines: [
                    { x0: -0.8, y0: 0.5, x1: 0.8, y1: 0.5, kind: 0 },
                    { x0: -0.8, y0: 0.4, x1: 0.8, y1: 0.4, kind: 0 },
                ],
                glyphs: [],
                notes: [],
                shouldClipNotesAtClefs: false,
            });

            assert.equal(scoreLayer.context.strokeCalls, scoreLayerStrokesAfterSceneRender + 3);
        } finally {
            dispose(canvas);
        }
    }, { onCreateElement: tagName => assert.equal(tagName, "canvas") });
});

test("score-only canvas owns independent resize, scene cache, and disposal state", () => {
    withCanvasMocks(({ createdCanvases, observers }) => {
        const primaryCanvas = new FakeCanvas();
        const waveformCanvas = new FakeCanvas();
        const spectrumCanvas = new FakeCanvas();
        const secondaryCanvas = new FakeCanvas();

        try {
            initialize(primaryCanvas, waveformCanvas, spectrumCanvas, { spectrumVisibleBinCount: 32 });
            initializeScoreCanvas(secondaryCanvas);
            render(primaryCanvas, {
                kind: 0,
                lines: [{ x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 }],
                glyphs: [],
                notes: [],
            });
            render(secondaryCanvas, {
                kind: 0,
                lines: [
                    { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
                    { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
                ],
                glyphs: [],
                notes: [],
            });

            assert.deepEqual(observers[0].observed, [primaryCanvas, waveformCanvas, spectrumCanvas]);
            assert.deepEqual(observers[1].observed, [secondaryCanvas]);
            assert.equal(createdCanvases[0].context.strokeCalls, 1);
            assert.equal(createdCanvases[1].context.strokeCalls, 2);

            dispose(secondaryCanvas);
            assert.equal(observers[1].isDisconnected, true);
            assert.equal(observers[0].isDisconnected, false);
        } finally {
            dispose(secondaryCanvas);
            dispose(primaryCanvas);
        }
    });
});

test("grand staff sizes signature glyphs to their requested heights", () => {
    const scoreContext = renderGrandStaffScene({
        kind: 0,
        lines: [],
        glyphs: [
            { text: "♯", x: -0.78, y: 0.3, kind: 2, height: 0.2 },
            { text: "4", x: -0.59, y: 0.2, kind: 3, height: 0.2 },
        ],
        notes: [],
        beams: [],
        shouldClipNotesAtClefs: false,
    });

    assert.equal(scoreContext.measureTextCalls, 4);
    assert.equal(scoreContext.fillTextCalls[0].args[0], "♯");
    assert.ok(Math.abs(Number.parseFloat(scoreContext.fillTextCalls[0].font) - 20.4) < 1e-9);
    assert.equal(scoreContext.fillTextCalls[1].args[0], "4");
    assert.ok(Math.abs(Number.parseFloat(scoreContext.fillTextCalls[1].font) - 20.4) < 1e-9);
});

test("grand staff draws octave-shift lines dashed and resets the dash pattern", () => {
    const scoreContext = renderGrandStaffScene({
        kind: 0,
        lines: [{ x0: -0.4, y0: 0.6, x1: 0.4, y1: 0.6, kind: octaveShiftLineKind }],
        glyphs: [],
        notes: [],
        beams: [],
        shouldClipNotesAtClefs: false,
    });

    assert.equal(scoreContext.lineDashCalls.length, 2);
    assert.ok(scoreContext.lineDashCalls[0].length > 0);
    assert.deepEqual(scoreContext.lineDashCalls[1], []);
});

test("grand staff clamps a flat glyph's computed font size instead of blowing it up", () => {
    // Regression test for a real bug found by screenshot-verifying the tenuto articulation mark
    // (an en dash "–") against the running app: its actualBoundingBoxAscent+Descent measures far
    // smaller than a normal letter/digit/musical-symbol glyph (a "flat" mark, centered near the
    // baseline), which inflated the computed font size unboundedly and rendered as an oversized
    // blob overlapping neighboring notes. drawGlyph now floors the measured-height divisor.
    const normalGlyph = { text: "X", x: 0, y: 0, kind: 5, height: 0.2 };
    const flatGlyph = { text: "-", x: 0, y: 0, kind: 5, height: 0.2 };
    const baseScene = { kind: 0, lines: [], notes: [], beams: [], shouldClipNotesAtClefs: false };

    const normalContext = renderGrandStaffScene(
        { ...baseScene, glyphs: [normalGlyph] },
        640,
        240,
        { X: { actualBoundingBoxAscent: 60, actualBoundingBoxDescent: 15, actualBoundingBoxRight: 40 } });
    const flatContext = renderGrandStaffScene(
        { ...baseScene, glyphs: [flatGlyph] },
        640,
        240,
        { "-": { actualBoundingBoxAscent: 5, actualBoundingBoxDescent: 1, actualBoundingBoxRight: 40 } });

    const normalFontSize = Number.parseFloat(normalContext.fillTextCalls[0].font);
    const flatFontSize = Number.parseFloat(flatContext.fillTextCalls[0].font);
    // Unclamped, this would be a 75/6 ≈ 12.5x blowup relative to the normal glyph. The floor
    // bounds it to 75/20 = 3.75x.
    assert.ok(
        flatFontSize <= normalFontSize * 4,
        `expected the flat glyph's font size (${flatFontSize}) to stay within a bounded multiple ` +
            `of the normal glyph's (${normalFontSize}), not blow up unbounded`);
});

test("grand staff colors an accidental glyph to match its note, not clef white", () => {
    const scoreContext = renderGrandStaffScene({
        kind: 0,
        lines: [],
        glyphs: [
            { text: "𝄞", x: -0.87, y: 0.2, kind: 0, height: 0.4 },
            { text: "♯", x: -0.5, y: 0.2, kind: 1, height: 0.1 },
            { text: "♯", x: -0.3, y: 0.2, kind: 1, height: 0.1, isActive: true },
            { text: "♯", x: -0.1, y: 0.2, kind: 1, height: 0.1, verdict: 0 },
        ],
        notes: [],
        beams: [],
        shouldClipNotesAtClefs: false,
    });

    assert.equal(scoreContext.fillTextCalls[0].fillStyle, "#e2e8f0"); // clef stays off-white
    assert.equal(scoreContext.fillTextCalls[1].fillStyle, "#fbbf24"); // default note amber
    assert.equal(scoreContext.fillTextCalls[2].fillStyle, "#22d3ee"); // active note cyan
    assert.equal(scoreContext.fillTextCalls[3].fillStyle, "#4ade80"); // Verdict.Correct
});

test("grand staff draws score beams", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [],
        glyphs: [],
        notes: [],
        beams: [{ x0: -0.5, y0: 0.2, x1: 0.5, y1: 0.3, count: 1, stemDirection: 0 }],
        shouldClipNotesAtClefs: false,
    });

    assert.equal(context.strokeCalls, 1);
});

test("grand staff draws compact angled noteheads", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [],
        glyphs: [],
        notes: [{
            x: 0,
            y: 0,
            isActive: false,
            isFilled: true,
            hasStem: false,
            hasDot: false,
            flagCount: 0,
            label: "C4",
        }],
        beams: [],
        shouldClipNotesAtClefs: false,
    });

    const ellipse = context.ellipseCalls[0];
    assert.equal(ellipse[2], 6.6);
    assert.equal(ellipse[3], 4.4);
    assert.equal(ellipse[4], -Math.PI / 8);
});

test("grand staff keeps a dotted stem-up note's dot clear of its stem", () => {
    const dottedNote = (stemDirection) => ({
        x: 0,
        y: 0,
        isActive: false,
        isFilled: false,
        hasStem: true,
        stemEndY: -0.3,
        stemDirection,
        hasDot: true,
        flagCount: 0,
        label: "F4",
    });
    const render = (note) => renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
            { x0: -0.8, y0: 0, x1: 0.8, y1: 0, kind: 0 },
        ],
        glyphs: [],
        notes: [note],
        beams: [],
        shouldClipNotesAtClefs: false,
    }, 1280);

    const stemUp = render(dottedNote(0));
    const stemDown = render(dottedNote(1));

    const staffSpace = Math.abs(stemUp.lineSegments[0].y - stemUp.lineSegments[1].y);
    const [upDotX, , upDotRadius] = stemUp.arcCalls[0];
    const upStemX = stemUp.lineSegments.at(-1).x;
    const [downDotX] = stemDown.arcCalls[0];
    const noteX = stemUp.ellipseCalls[0][0];
    // The dot's left edge must leave a visible gap (at least a quarter of a staff space) to the stem on its left.
    assert.ok(upDotX - upDotRadius - upStemX >= staffSpace * 0.25 - 1e-9,
        `dot edge ${upDotX - upDotRadius} is too close to the stem at ${upStemX}`);
    // A stem-down note has its stem on the other side, so its dot keeps the usual closer offset.
    assert.ok(downDotX - noteX < upDotX - noteX);
});

test("grand staff draws pitch labels on the supplied shared row", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.4, x1: 0.8, y1: 0.4, kind: 0 },
            { x0: -0.8, y0: 0.3, x1: 0.8, y1: 0.3, kind: 0 },
        ],
        glyphs: [],
        notes: [
            { x: -0.2, y: 0.35, labelY: 0.1, isActive: false, isFilled: true, label: "A4" },
            { x: 0.2, y: 0.55, labelY: 0.1, isActive: false, isFilled: true, label: "F#5" },
        ],
        beams: [],
        shouldClipNotesAtClefs: false,
    });

    const labelYs = context.fillTextCalls.map(call => call.args[2]);
    assert.equal(labelYs.length, 2);
    assert.equal(labelYs[0], labelYs[1]);
});

// A note-name label is 16 px text centred under its note. Labels of neighbouring notes in one row that would touch are
// drawn smaller (down to a floor), so a run of three-character names (F#4 Eb4 C#4) stays readable at a narrow width.
// The fake context measures text at half the font size per character, and 640 px is 302 px per scene-X unit.
function labelScene(notes) {
    return {
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.4, x1: 0.8, y1: 0.4, kind: 0 },
            { x0: -0.8, y0: 0.3, x1: 0.8, y1: 0.3, kind: 0 },
        ],
        glyphs: [],
        notes: notes.map(note => ({ y: 0.35, labelY: 0.1, isActive: false, isFilled: true, ...note })),
        beams: [],
        shouldClipNotesAtClefs: false,
    };
}

function labelFontSizes(context) {
    return context.fillTextCalls.map(call => ({ text: call.args[0], size: Number.parseFloat(call.font) }));
}

test("grand staff draws a label at full size when its neighbours leave room", () => {
    const context = renderGrandStaffScene(labelScene([
        { x: -0.2, label: "F#4" },
        { x: 0.2, label: "Eb4" },
    ]));

    assert.deepEqual(labelFontSizes(context), [{ text: "F#4", size: 16 }, { text: "Eb4", size: 16 }]);
});

test("grand staff shrinks labels that would touch their neighbour so they keep a gap", () => {
    // 0.04 either side of the centre is 24.16 px between the labels; three characters at 16 px are 24 px wide.
    const context = renderGrandStaffScene(labelScene([
        { x: -0.04, label: "F#4" },
        { x: 0.04, label: "Eb4" },
    ]));

    const expectedSize = 16 * (24.16 - 2) / 24;
    const sizes = labelFontSizes(context);
    assert.equal(sizes.length, 2);
    for (const { size } of sizes) {
        assert.ok(Math.abs(size - expectedSize) < 0.01, `expected ${expectedSize}, got ${size}`);
        // The two labels together now leave at least the minimum gap between them.
        assert.ok((size / 16 * 24 / 2) * 2 + 2 <= 24.16 + 1e-9);
    }
});

test("grand staff never shrinks a label below its floor, however close the neighbour", () => {
    const context = renderGrandStaffScene(labelScene([
        { x: -0.01, label: "F#4" },
        { x: 0.01, label: "Eb4" },
    ]));

    for (const { size } of labelFontSizes(context)) {
        assert.ok(Math.abs(size - 8) < 1e-9, `expected the 8 px floor (half the normal size), got ${size}`);
    }
});

test("grand staff only compares labels in the same row", () => {
    // Same x, different rows: a chord's stacked names never shrink each other.
    const context = renderGrandStaffScene(labelScene([
        { x: 0, label: "G4", labelY: 0.1 },
        { x: 0, label: "E4", labelY: 0.04 },
    ]));

    assert.deepEqual(labelFontSizes(context).map(label => label.size), [16, 16]);
});

test("grand staff leaves labels of the same moment at their own size", () => {
    // 0.01 scene-X is 3 px: two notes of one chord or a live note over another, which shrinking cannot separate.
    const context = renderGrandStaffScene(labelScene([
        { x: 0, label: "E4" },
        { x: 0.01, label: "C4" },
    ]));

    assert.deepEqual(labelFontSizes(context).map(label => label.size), [16, 16]);
});

test("grand staff fits a label by its own neighbours' distance, so only the crowded one shrinks", () => {
    // Left pair 24.16 px apart (crowded), right neighbour 200 px away: the middle label follows its nearest neighbour.
    const context = renderGrandStaffScene(labelScene([
        { x: -0.04, label: "F#4" },
        { x: 0.04, label: "Eb4" },
        { x: 0.7, label: "C#5" },
    ]));

    const sizes = Object.fromEntries(labelFontSizes(context).map(label => [label.text, label.size]));
    assert.ok(sizes["F#4"] < 16);
    assert.ok(sizes["Eb4"] < 16);
    assert.equal(sizes["C#5"], 16);
});

test("grand staff applies the fit on top of a label's own severe-stack scale", () => {
    const context = renderGrandStaffScene(labelScene([
        { x: -0.04, label: "F#4", labelFontScale: 0.75 },
        { x: 0.04, label: "Eb4", labelFontScale: 0.75 },
    ]));

    // At 0.75 x 16 px a three-character name is 18 px wide, which fits 24.16 px with the gap: left alone.
    assert.deepEqual(labelFontSizes(context).map(label => label.size), [12, 12]);
});

test("score playback highlight redraws a crowded label at the same fitted size", async () => {
    const harness = await createScoreCursorHarness(1);
    const canvas = harness.createCanvas();
    const noteX = mapScoreNotationBeatToX(1, 4, 0);
    const note = { y: 0.2, labelY: 0.1, isActive: false, isFilled: true, scoreOnsetBeats: 1, scoreEndBeats: 2 };

    try {
        render(canvas, {
            kind: 0,
            lines: [
                { x0: -0.8, y0: 0.3, x1: 0.8, y1: 0.3, kind: 0 },
                { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            ],
            glyphs: [],
            notes: [
                { ...note, x: noteX, label: "F#4" },
                { ...note, x: noteX + 0.04, label: "Eb4", scoreOnsetBeats: 2, scoreEndBeats: 3 },
            ],
            beams: [],
            shouldClipNotesAtClefs: false,
        });
        startScoreCursor(canvas, {
            anchorSeconds: 0,
            beatsPerMinute: 60,
            beatsPerMeasure: 4,
            firstVisibleMeasure: 0,
            completionSeconds: 5,
            cursorY0: -0.5,
            cursorY1: 0.5,
            visibleMeasureCount: 5,
        });

        const labelCalls = canvas.context.fillTextCalls.filter(call => call.args[0] === "F#4");
        assert.equal(labelCalls.length, 1, "the highlight redraws the highlighted note's label once on the main canvas");
        assert.ok(Number.parseFloat(labelCalls[0].font) < 16, `highlighted label font ${labelCalls[0].font} was not fitted`);
    } finally {
        await harness.dispose();
    }
});

test("grand staff ledger lines extend visibly beyond noteheads", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
            { x0: -0.8, y0: 0, x1: 0.8, y1: 0, kind: 0 },
            { x0: -0.065, y0: -0.1, x1: 0.065, y1: -0.1, kind: 1 },
        ],
        glyphs: [],
        notes: [{ x: 0, y: -0.1, isActive: false, isFilled: true, label: "C#4" }],
        beams: [],
        shouldClipNotesAtClefs: false,
    }, 1280);

    const [firstStaffLine, secondStaffLine, ledgerLine] = context.lineSegments;
    const staffSpace = Math.abs(firstStaffLine.y - secondStaffLine.y);
    const ledgerLineLength = Math.abs(ledgerLine.x1 - ledgerLine.x);
    const noteHeadWidth = context.ellipseCalls[0][2] * 2;
    assert.ok(ledgerLineLength - noteHeadWidth >= (staffSpace * 0.8) - 1e-9);
    const noteHeadOperationIndex = context.operations
        .findIndex(operation => operation.kind === "ellipse");
    const ledgerLineOperationIndex = context.operations
        .findIndex(operation => operation.kind === "line"
            && operation.segment.y === operation.segment.y1
            && Math.abs(operation.segment.x1 - operation.segment.x) === ledgerLineLength);
    assert.ok(ledgerLineOperationIndex > noteHeadOperationIndex);
});

test("grand staff draws clipped full ties and edge stubs behind noteheads", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        ],
        glyphs: [{ text: "𝄞", x: -0.87, y: 0.2, kind: 0, height: 0.4 }],
        notes: [
            { x: -0.2, y: 0, isActive: false, isFilled: true, label: "C4" },
            { x: 0.2, y: 0, isActive: false, isFilled: true, label: "C4" },
        ],
        beams: [],
        ties: [
            { x0: -0.2, y0: 0, x1: 0.2, y1: 0, curveDirection: 1, isActive: false },
            { x0: -0.56, y0: 0, x1: -0.535, y1: 0, curveDirection: 0, isActive: true },
        ],
        shouldClipNotesAtClefs: true,
    });

    assert.equal(context.filledPathCalls.length, 2);
    const [fullTie, incomingStub] = context.filledPathCalls;
    const staffSpace = Math.abs(context.lineSegments[0].y - context.lineSegments[1].y);
    const fullTieStartCenterX = 18 + (0.8 / 2) * 604;
    const fullTieEndCenterX = 18 + (1.2 / 2) * 604;
    assert.ok(Math.abs(fullTie.start.x - (fullTieStartCenterX + staffSpace * 0.8)) < 1e-9);
    assert.ok(Math.abs(fullTie.bezierCurves[0].x1 - (fullTieEndCenterX - staffSpace * 0.8)) < 1e-9);
    assert.ok(Math.abs(fullTie.start.y - (120 + staffSpace * 0.12)) < 1e-9);
    assert.equal(fullTie.bezierCurves.length, 2);
    assert.ok(fullTie.isClosed);
    assert.ok(incomingStub.bezierCurves[0].controlY1 < incomingStub.start.y);
    assert.ok(incomingStub.bezierCurves[0].x1 > incomingStub.start.x);
    assert.ok(incomingStub.bezierCurves[0].x1 < 18 + ((-0.535 + 1) / 2) * 604);
    assert.equal(fullTie.fillStyle, "#fbbf24");
    assert.equal(incomingStub.fillStyle, "#22d3ee");
    assert.ok(context.filledPathCalls.every(path => path.clipCalls > 0));
    assert.ok(Math.abs(context.rectCalls[0][0] - (18 + (0.44 / 2) * 604)) < 1e-9);
    assert.ok(
        context.operations.findIndex(operation => operation.kind === "filledPath")
        < context.operations.findIndex(operation => operation.kind === "ellipse"));
});

test("grand staff tie height and tapered thickness derive from staff spacing", () => {
    const scene = {
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        ],
        glyphs: [],
        notes: [],
        beams: [],
        ties: [{ x0: -0.2, y0: 0, x1: 0.2, y1: 0, curveDirection: 1, isActive: false }],
        shouldClipNotesAtClefs: false,
    };

    // Tall canvases (28 px staff space), where 0.08 staff space is above the 1.2 px minimum thickness: this test pins the
    // staff-space rule itself, the minimum is pinned by the "at least 1.2 px thick" tests.
    const narrowContext = renderGrandStaffScene(scene, 640, 600);
    const wideContext = renderGrandStaffScene(scene, 1280, 600);
    const narrowTie = narrowContext.filledPathCalls[0];
    const wideTie = wideContext.filledPathCalls[0];
    const staffSpace = Math.abs(
        narrowContext.lineSegments[0].y - narrowContext.lineSegments[1].y);
    const firstArc = narrowTie.bezierCurves[0];
    const returnArc = narrowTie.bezierCurves[1];
    const centerControlY = (firstArc.controlY1 + returnArc.controlY2) / 2;
    const apexHeight = Math.abs(centerControlY - narrowTie.start.y) * 0.75;
    const centerThickness = Math.abs(firstArc.controlY1 - returnArc.controlY2) * 0.75;
    const wideFirstArc = wideTie.bezierCurves[0];
    const wideReturnArc = wideTie.bezierCurves[1];
    const wideCenterControlY = (wideFirstArc.controlY1 + wideReturnArc.controlY2) / 2;
    const wideApexHeight = Math.abs(wideCenterControlY - wideTie.start.y) * 0.75;
    const wideCenterThickness = Math.abs(wideFirstArc.controlY1 - wideReturnArc.controlY2) * 0.75;

    assert.ok(apexHeight >= staffSpace * 0.3 - 1e-9);
    assert.ok(apexHeight <= staffSpace * 0.45 + 1e-9);
    assert.ok(wideApexHeight >= apexHeight);
    assert.ok(wideApexHeight <= staffSpace * 0.45 + 1e-9);
    assert.ok(Math.abs(centerThickness - (staffSpace * 0.08)) < 1e-9);
    assert.ok(Math.abs(wideCenterThickness - (staffSpace * 0.08)) < 1e-9);
});

// The thickest point of a tie, from the control points of its two bezier arcs (each shifted by 2/3 of the thickness).
function tieThickness(filledTie) {
    const [firstArc, returnArc] = filledTie.bezierCurves;
    return Math.abs(firstArc.controlY1 - returnArc.controlY2) * 0.75;
}

// Real scene geometry of the exercise score view: staff lines 0.0738 scene units apart, on the smallest clamped canvas
// (916 x 240), which makes a staff space 7.5 px.
const scoreViewTieScene = ties => ({
    kind: 0,
    lines: [
        { x0: -0.92, y0: 0.5155, x1: 0.96, y1: 0.5155, kind: 0 },
        { x0: -0.92, y0: 0.4417, x1: 0.96, y1: 0.4417, kind: 0 },
    ],
    glyphs: [],
    notes: [],
    beams: [],
    ties,
    shouldClipNotesAtClefs: false,
});

test("grand staff ties stay at least 1.2 px thick on the smallest score canvas", () => {
    const context = renderGrandStaffScene(
        scoreViewTieScene([{ x0: -0.2, y0: 0.45, x1: 0.2, y1: 0.45, curveDirection: 1, isActive: false }]),
        916,
        240);

    const staffSpace = Math.abs(context.lineSegments[0].y - context.lineSegments[1].y);
    assert.ok(Math.abs(staffSpace - 7.5) < 0.05, `staff space ${staffSpace}`);
    assert.ok(
        tieThickness(context.filledPathCalls[0]) >= 1.2 - 1e-9,
        `thickness ${tieThickness(context.filledPathCalls[0])} px`);
});

test("grand staff ties on the free-play canvas keep their staff-space thickness", () => {
    // The free-play (live) staff uses the same line spacing on a 460 px canvas at a 1000 px viewport and 544 px at its
    // largest: 15.6 px and 18.5 px staff spaces, where 0.08 staff space (1.25 px and 1.5 px) already exceeds the minimum.
    for (const [height, expectedStaffSpace] of [[460, 15.6], [544, 18.7]]) {
        const context = renderGrandStaffScene(
            scoreViewTieScene([{ x0: -0.2, y0: 0.45, x1: 0.2, y1: 0.45, curveDirection: 1, isActive: false }]),
            916,
            height);

        const staffSpace = Math.abs(context.lineSegments[0].y - context.lineSegments[1].y);
        assert.ok(Math.abs(staffSpace - expectedStaffSpace) < 0.2, `staff space ${staffSpace} at ${height}`);
        assert.ok(
            Math.abs(tieThickness(context.filledPathCalls[0]) - (staffSpace * 0.08)) < 1e-9,
            `thickness ${tieThickness(context.filledPathCalls[0])} at ${height} px`);
    }
});

test("grand staff keeps a tie's tapered shape and endpoint gaps when it thickens it", () => {
    const tie = { x0: -0.2, y0: 0.45, x1: 0.2, y1: 0.45, curveDirection: 1, isActive: false };
    for (const height of [240, 900]) {
        const context = renderGrandStaffScene(scoreViewTieScene([tie]), 916, height);
        const filled = context.filledPathCalls[0];
        const staffSpace = Math.abs(context.lineSegments[0].y - context.lineSegments[1].y);
        const startCenterX = 18 + (0.8 / 2) * (916 - 36);
        const endCenterX = 18 + (1.2 / 2) * (916 - 36);

        // Both arcs run between the same two points (a lens, zero thickness at the ends), 0.8 staff space from the note
        // centres: only the control points move when the tie is thickened.
        assert.equal(filled.bezierCurves.length, 2);
        assert.ok(filled.isClosed);
        assert.ok(Math.abs(filled.start.x - (startCenterX + staffSpace * 0.8)) < 1e-9, `height ${height}`);
        assert.ok(Math.abs(filled.bezierCurves[0].x1 - (endCenterX - staffSpace * 0.8)) < 1e-9, `height ${height}`);
        assert.equal(filled.bezierCurves[0].x1, filled.bezierCurves[1].x);
        assert.equal(filled.bezierCurves[0].y1, filled.bezierCurves[1].y);
        assert.equal(filled.bezierCurves[1].x1, filled.start.x);
        assert.equal(filled.bezierCurves[1].y1, filled.start.y);
    }
});

test("grand staff starts the tie that arrives at a row at the opening barline, with no gap before it", () => {
    // The scene's X values are float-derived: ScoreX0 (-0.56f) - 0.02 arrives as -0.5800000023841858.
    const arriving = { x0: -0.5800000023841858, y0: 0.45, x1: -0.54, y1: 0.45, curveDirection: 0, isActive: false };
    const inRow = { x0: -0.3, y0: 0.45, x1: -0.26, y1: 0.45, curveDirection: 0, isActive: false };

    const arrivingContext = renderGrandStaffScene(scoreViewTieScene([arriving]), 916, 240);
    const inRowContext = renderGrandStaffScene(scoreViewTieScene([inRow]), 916, 240);

    const staffSpace = Math.abs(arrivingContext.lineSegments[0].y - arrivingContext.lineSegments[1].y);
    const arrivingStart = arrivingContext.filledPathCalls[0].start.x;
    const inRowStart = inRowContext.filledPathCalls[0].start.x;
    assert.ok(Math.abs(arrivingStart - (18 + (0.42 / 2) * 880)) < 1e-3, `starts at ${arrivingStart}`);
    assert.ok(Math.abs(inRowStart - (18 + (0.7 / 2) * 880 + staffSpace * 0.8)) < 1e-3, `starts at ${inRowStart}`);
    // It ends the usual 0.8 staff space short of its note, and is long enough to see.
    const arrivingEnd = arrivingContext.filledPathCalls[0].bezierCurves[0].x1;
    assert.ok(Math.abs(arrivingEnd - (18 + (0.46 / 2) * 880 - staffSpace * 0.8)) < 1e-3);
    assert.ok(arrivingEnd - arrivingStart >= 10, `${arrivingEnd - arrivingStart} px long`);
});

test("grand staff ties arriving from the left edge are at least 1.2 px thick too", () => {
    // The half tie of a note at the start of the next row: from the score's left edge to the first note.
    const context = renderGrandStaffScene(
        scoreViewTieScene([{ x0: -0.56, y0: 0.45, x1: -0.535, y1: 0.45, curveDirection: 0, isActive: false }]),
        916,
        240);

    assert.ok(tieThickness(context.filledPathCalls[0]) >= 1.2 - 1e-9);
});

test("grand staff scenes without ties keep their previous canvas operations", () => {
    const scene = {
        kind: 0,
        lines: [{ x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 }],
        glyphs: [],
        notes: [{ x: 0, y: 0, isActive: false, isFilled: true, label: "C4" }],
        beams: [],
        shouldClipNotesAtClefs: false,
    };

    const omittedTies = renderGrandStaffScene(scene);
    const emptyTies = renderGrandStaffScene({ ...scene, ties: [] });

    assert.equal(omittedTies.curveCalls.length, 0);
    assert.equal(emptyTies.curveCalls.length, 0);
    assert.equal(omittedTies.filledPathCalls.length, 0);
    assert.equal(emptyTies.filledPathCalls.length, 0);
    assert.equal(omittedTies.strokeCalls, emptyTies.strokeCalls);
    assert.equal(omittedTies.ellipseCalls.length, emptyTies.ellipseCalls.length);
    assert.deepEqual(omittedTies.lineSegments, emptyTies.lineSegments);
});

test("grand staff draws a slur as a thin stroked arc, distinct from a tie's filled shape", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        ],
        glyphs: [],
        notes: [
            { x: -0.2, y: 0, isActive: false, isFilled: true, label: "C4" },
            { x: 0.2, y: 0.05, isActive: false, isFilled: true, label: "E4" },
        ],
        beams: [],
        slurs: [{ x0: -0.2, y0: 0, x1: 0.2, y1: 0.05, curveDirection: 1 }],
        shouldClipNotesAtClefs: false,
    });

    assert.equal(context.filledPathCalls.length, 0);
    const slurStroke = context.strokedPathCalls[0];
    assert.ok(slurStroke, "expected a stroked bezier path for the slur");
    assert.equal(slurStroke.bezierCurves.length, 1);
    assert.equal(slurStroke.start.x, 18 + (0.8 / 2) * 604);
});

test("grand staff slurs curve away from the note group, opposite a downward automatic stem", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        ],
        glyphs: [],
        notes: [],
        beams: [],
        // curveDirection 0 = StemDirection.Up: canvas.js bows the curve toward negative scene-Y
        // (screen-down) when the direction constant is Up (see drawTie's identical convention).
        slurs: [{ x0: -0.2, y0: 0, x1: 0.2, y1: 0, curveDirection: 0 }],
        shouldClipNotesAtClefs: false,
    });

    const slurStroke = context.strokedPathCalls[0];
    const midpointY = (slurStroke.start.y + slurStroke.bezierCurves[0].y1) / 2;
    const curveMidY = (slurStroke.bezierCurves[0].controlY1 + slurStroke.bezierCurves[0].controlY2) / 2;
    assert.ok(curveMidY < midpointY);
});

test("grand staff scenes without slurs keep their previous canvas operations", () => {
    const scene = {
        kind: 0,
        lines: [{ x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 }],
        glyphs: [],
        notes: [{ x: 0, y: 0, isActive: false, isFilled: true, label: "C4" }],
        beams: [],
        shouldClipNotesAtClefs: false,
    };

    const omittedSlurs = renderGrandStaffScene(scene);
    const emptySlurs = renderGrandStaffScene({ ...scene, slurs: [] });

    assert.equal(omittedSlurs.strokedPathCalls.length, 0);
    assert.equal(emptySlurs.strokedPathCalls.length, 0);
    assert.equal(omittedSlurs.strokeCalls, emptySlurs.strokeCalls);
});

test("grand staff draws an arpeggiate mark as a wavy line using curves", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        ],
        glyphs: [],
        notes: [],
        beams: [],
        arpeggioMarks: [{ x: -0.3, y0: -0.1, y1: 0.1, isNonArpeggiate: false }],
        shouldClipNotesAtClefs: false,
    });

    assert.ok(context.curveCalls.length > 0, "expected the wavy arpeggiate mark to use curves");
});

test("grand staff draws a non-arpeggiate mark as a straight bracket, distinct from the wavy line", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        ],
        glyphs: [],
        notes: [],
        beams: [],
        arpeggioMarks: [{ x: -0.3, y0: -0.1, y1: 0.1, isNonArpeggiate: true }],
        shouldClipNotesAtClefs: false,
    });

    assert.equal(context.curveCalls.length, 0, "a bracket should not use any curves");
    assert.ok(context.lineSegments.length > 0, "expected straight bracket line segments");
});

test("grand staff draws a glissando/slide line straight between noteheads, distinct from other line kinds", () => {
    const context = renderGrandStaffScene({
        kind: 0,
        lines: [
            { x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 },
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
            { x0: -0.2, y0: 0, x1: 0.2, y1: 0.05, kind: glissandoLineKind },
        ],
        glyphs: [],
        notes: [],
        beams: [],
        shouldClipNotesAtClefs: false,
    });

    const glissandoSegment = context.lineSegments.find(
        segment => segment.x === mapXForTest(-0.2) && segment.x1 === mapXForTest(0.2));
    assert.ok(glissandoSegment, "expected a straight line segment between the two note X positions");
});

function mapXForTest(value) {
    const padding = 18;
    return padding + ((value + 1) / 2) * Math.max(0, 640 - (padding * 2));
}

test("grand staff scenes without arpeggio marks keep their previous canvas operations", () => {
    const scene = {
        kind: 0,
        lines: [{ x0: -0.8, y0: 0.2, x1: 0.8, y1: 0.2, kind: 0 }],
        glyphs: [],
        notes: [{ x: 0, y: 0, isActive: false, isFilled: true, label: "C4" }],
        beams: [],
        shouldClipNotesAtClefs: false,
    };

    const omittedMarks = renderGrandStaffScene(scene);
    const emptyMarks = renderGrandStaffScene({ ...scene, arpeggioMarks: [] });

    assert.equal(omittedMarks.curveCalls.length, emptyMarks.curveCalls.length);
    assert.equal(omittedMarks.strokeCalls, emptyMarks.strokeCalls);
});

function reviewMarkScene(notes, { glyphs = [], bands = [], ties = [], lines = undefined } = {}) {
    return {
        kind: 0,
        lines: lines ?? [
            { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
            { x0: -0.8, y0: 0, x1: 0.8, y1: 0, kind: 0 },
        ],
        glyphs,
        notes: notes.map(note => ({ isActive: false, isFilled: true, hasStem: false, flagCount: 0, label: "C4", ...note })),
        beams: [],
        ties,
        bands,
        shouldClipNotesAtClefs: false,
    };
}

const reviewMarkClean = 0;
const reviewMarkTiming = 1;
const reviewMarkPitch = 2;
const reviewMarkMissed = 3;

// The halos are the stroked rounded rectangles (the annotation bands are filled ones).
function ringsOf(context) {
    return context.roundRectCalls.filter(call => call.stroked);
}

test("review marks draw one ring per marked note and nothing for a clean or unmarked note", () => {
    const context = renderGrandStaffScene(reviewMarkScene([
        { x: -0.4, y: 0, reviewMark: reviewMarkTiming, reviewMarkGroup: 0 },
        { x: -0.2, y: 0, reviewMark: reviewMarkClean, reviewMarkGroup: 1 },
        { x: 0, y: 0, reviewMark: reviewMarkMissed, reviewMarkGroup: 2 },
        { x: 0.2, y: 0 },
    ]));

    assert.equal(ringsOf(context).length, 2);
    // Both rings are drawn around a head, and the heads themselves are still drawn.
    assert.equal(context.ellipseCalls.length, 4);
});

test("review marks leave scenes without marks (or with only clean marks) exactly as they were", () => {
    const notes = [{ x: -0.2, y: 0 }, { x: 0.2, y: 0 }];
    const plain = renderGrandStaffScene(reviewMarkScene(notes));
    const allClean = renderGrandStaffScene(reviewMarkScene(notes.map((note, index) => ({
        ...note,
        reviewMark: reviewMarkClean,
        reviewMarkGroup: index,
    }))));

    assert.equal(ringsOf(allClean).length, 0);
    assert.deepEqual(allClean.operations, plain.operations);
    assert.equal(allClean.strokeCalls, plain.strokeCalls);
});

test("review marks are shape coded as well as colored: thick pitch ring, dashed missed ring, plain timing ring", () => {
    const context = renderGrandStaffScene(reviewMarkScene([
        { x: -0.4, y: 0, reviewMark: reviewMarkTiming, reviewMarkGroup: 0 },
        { x: 0, y: 0, reviewMark: reviewMarkPitch, reviewMarkGroup: 1 },
        { x: 0.4, y: 0, reviewMark: reviewMarkMissed, reviewMarkGroup: 2 },
    ]));

    const [timing, pitch, missed] = ringsOf(context);
    assert.deepEqual(timing.lineDash, []);
    assert.deepEqual(pitch.lineDash, []);
    assert.ok(missed.lineDash.length >= 2 && missed.lineDash.every(length => length > 0));
    assert.ok(pitch.lineWidth > timing.lineWidth);
    assert.equal(new Set([timing.strokeStyle, pitch.strokeStyle, missed.strokeStyle]).size, 3);
});

test("review marks draw a ring around the heads of a chord as one halo", () => {
    const context = renderGrandStaffScene(reviewMarkScene([
        { x: 0, y: 0.1, reviewMark: reviewMarkPitch, reviewMarkGroup: 4 },
        { x: 0, y: 0, reviewMark: reviewMarkPitch, reviewMarkGroup: 4 },
        { x: 0, y: -0.1, reviewMark: reviewMarkPitch, reviewMarkGroup: 4 },
    ]));

    assert.equal(ringsOf(context).length, 1);
    const ring = ringsOf(context)[0];
    const headCenters = context.ellipseCalls.map(call => call[1]);
    const headHeight = context.ellipseCalls[0][3] * 2;
    assert.ok(ring.y < Math.min(...headCenters) - (headHeight / 2));
    assert.ok(ring.y + ring.height > Math.max(...headCenters) + (headHeight / 2));
});

test("review marks give two groups at the same beat on different staves a ring each", () => {
    const context = renderGrandStaffScene(reviewMarkScene([
        { x: 0, y: 0.5, reviewMark: reviewMarkTiming, reviewMarkGroup: 0 },
        { x: 0, y: -0.5, reviewMark: reviewMarkTiming, reviewMarkGroup: 1 },
    ]));

    assert.equal(ringsOf(context).length, 2);
});

test("review marks wrap a note's accidental instead of cutting through it", () => {
    const accidentalX = -0.019;
    const context = renderGrandStaffScene(reviewMarkScene(
        [{ x: 0, y: 0, reviewMark: reviewMarkTiming, reviewMarkGroup: 0 }],
        { glyphs: [{ text: "♯", x: accidentalX, y: 0, kind: 1, height: 0.1 }] }));

    const ring = ringsOf(context)[0];
    const accidentalCenter = mapXForTest(accidentalX);
    assert.ok(ring.x < accidentalCenter, "the ring must start left of the accidental's center");
    assert.ok(ring.x + (ring.lineWidth / 2) < accidentalCenter - 1);
});

test("review marks stay clear of an annotation strip and never draw over their own head", () => {
    // The head sits about 4 px above the strip, so there is no room for a full ring below it.
    const staffSpace = 10.2;
    const headY = 0.0;
    const stripTopY = headY - ((staffSpace * 0.4 + 4) / 102);
    const context = renderGrandStaffScene(reviewMarkScene(
        [{ x: 0, y: headY, reviewMark: reviewMarkPitch, reviewMarkGroup: 0 }],
        { bands: [{ x0: -0.9, y0: stripTopY, x1: 0.9, y1: stripTopY - 0.3 }] }));

    const ring = ringsOf(context)[0];
    const stripTop = context.ellipseCalls[0][1] + (staffSpace * 0.4 + 4);
    const headBottom = context.ellipseCalls[0][1] + context.ellipseCalls[0][3];
    assert.ok(ring.y + ring.height + (ring.lineWidth / 2) < stripTop, "the ring's outer edge must stay above the strip");
    assert.ok(ring.y + ring.height - (ring.lineWidth / 2) >= headBottom - 1e-9, "the ring must not cut into the head");
});

test("review marks stay off a neighbouring head, and share the space with a neighbour's own ring", () => {
    const neighbourX = 0.0736; // 22.2 px between centers: the heads are 12.2 px wide, so 10 px of free space
    const unmarkedNeighbour = renderGrandStaffScene(reviewMarkScene([
        { x: 0, y: 0, reviewMark: reviewMarkPitch, reviewMarkGroup: 0 },
        { x: neighbourX, y: 0 },
    ]));
    const markedNeighbour = renderGrandStaffScene(reviewMarkScene([
        { x: 0, y: 0, reviewMark: reviewMarkPitch, reviewMarkGroup: 0 },
        { x: neighbourX, y: 0, reviewMark: reviewMarkPitch, reviewMarkGroup: 1 },
    ]));

    const neighbourHeadLeft = unmarkedNeighbour.ellipseCalls[1][0] - unmarkedNeighbour.ellipseCalls[1][2];
    const [ring] = ringsOf(unmarkedNeighbour);
    assert.ok(ring.x + ring.width + (ring.lineWidth / 2) <= neighbourHeadLeft, "ring touches the neighbouring head");
    const [first, second] = ringsOf(markedNeighbour);
    assert.ok(first.x + first.width + (first.lineWidth / 2) <= second.x - (second.lineWidth / 2) + 1e-9,
        "two neighbouring rings overlap");
});

test("review marks stay off barlines and rests", () => {
    const barlineX = -0.0334; // 4 px left of the head's left edge, as for the first note of a measure
    const lines = [
        { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
        { x0: -0.8, y0: 0, x1: 0.8, y1: 0, kind: 0 },
        { x0: barlineX, y0: 0.2, x1: barlineX, y1: -0.2, kind: 2 },
    ];
    const context = renderGrandStaffScene(reviewMarkScene(
        [{ x: 0, y: 0, reviewMark: reviewMarkTiming, reviewMarkGroup: 0 }],
        { lines, glyphs: [{ text: "𝄽", x: 0.0736, y: 0.05, kind: 10, height: 0.12 }] }));

    const ring = ringsOf(context)[0];
    assert.ok(ring.x - (ring.lineWidth / 2) >= mapXForTest(barlineX), "the ring crosses the barline");
    const restHalfWidth = (0.12 / 2 * 204) * 0.2;
    assert.ok(ring.x + ring.width + (ring.lineWidth / 2) <= mapXForTest(0.0736) - restHalfWidth + 1e-9,
        "the ring reaches into the rest");
});

test("review marks are drawn under the noteheads and the ties", () => {
    const context = renderGrandStaffScene(reviewMarkScene(
        [
            { x: -0.2, y: 0, reviewMark: reviewMarkPitch, reviewMarkGroup: 0 },
            { x: 0.2, y: 0 },
        ],
        { ties: [{ x0: -0.2, y0: 0, x1: 0.2, y1: 0, curveDirection: 1, isActive: false }] }));

    const ringIndex = context.operations.findIndex(operation => operation.kind === "roundRect");
    const firstHeadIndex = context.operations.findIndex(operation => operation.kind === "ellipse");
    const tieIndex = context.operations.findIndex(operation => operation.kind === "filledPath");
    assert.ok(ringIndex >= 0 && ringIndex < firstHeadIndex, "the ring must be drawn before any notehead");
    assert.ok(ringIndex < tieIndex, "the ring must be drawn before the tie");
});

test("review marks never use the verdict channel", () => {
    // A note that carries both a live verdict (Late, orange) and a Pitch review mark gets the mark's own ring color,
    // not the verdict's.
    const context = renderGrandStaffScene(reviewMarkScene([
        { x: 0, y: 0, verdict: 3, reviewMark: reviewMarkPitch, reviewMarkGroup: 0 },
    ]));

    assert.equal(ringsOf(context).length, 1);
    assert.equal(ringsOf(context)[0].strokeStyle, "#f87171");
    assert.notEqual(ringsOf(context)[0].strokeStyle, verdictColors[3]);
});
