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
    render,
    startScoreCursor,
    stopScoreCursor,
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
    curveCalls = [];
    filledPathCalls = [];
    strokedPathCalls = [];
    fillTextCalls = [];
    lineSegments = [];
    measureTextCalls = 0;
    clipCalls = 0;
    operations = [];
    rectCalls = [];
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
    fillText(...args) {
        this.fillTextCalls.push({ args, font: this.font, fillStyle: this.fillStyle });
    }
    measureText(text) {
        this.measureTextCalls++;
        return this.metricsByText[text] ?? {
            actualBoundingBoxAscent: 80,
            actualBoundingBoxDescent: 20,
            actualBoundingBoxRight: 50,
        };
    }
    save() { }
    translate() { }
    rotate() { }
    restore() { }
    beginPath() {
        this.pathStart = undefined;
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
    clip() {
        this.clipCalls++;
    }
    arc() { }
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

    const narrowContext = renderGrandStaffScene(scene, 640, 240);
    const wideContext = renderGrandStaffScene(scene, 1280, 240);
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
