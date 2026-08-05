import assert from "node:assert/strict";
import test from "node:test";

import {
    dispose,
    initialize,
    initializeScoreCanvas,
    mapAbsoluteBeatToScoreX,
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

class FakeCanvasContext {
    strokeCalls = 0;
    drawImageCalls = 0;
    ellipseCalls = [];
    curveCalls = [];
    filledPathCalls = [];
    fillTextCalls = [];
    lineSegments = [];
    measureTextCalls = 0;
    clipCalls = 0;
    operations = [];
    rectCalls = [];
    pathStart = undefined;
    currentPath = undefined;

    setTransform() { }
    clearRect() { }
    fillRect() { }
    strokeRect() { }
    fillText(...args) {
        this.fillTextCalls.push({ args, font: this.font });
    }
    measureText() {
        this.measureTextCalls++;
        return {
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
            this.lineSegments.push({ ...this.pathStart, x1: x, y1: y });
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
    const noteX = mapAbsoluteBeatToScoreX(1, 4, 0);
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

function renderGrandStaffScene(scene, width = 640, height = 240) {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement() {
            const createdCanvas = new FakeCanvas();
            createdCanvas.clientWidth = width;
            createdCanvas.clientHeight = height;
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

    const canvas = new FakeCanvas();
    canvas.clientWidth = width;
    canvas.clientHeight = height;
    try {
        initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
        render(canvas, scene);
        return createdCanvases[0].context;
    } finally {
        dispose(canvas);
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
}

test("grand staff drawing caches its static layer until the scene or size changes", () => {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const resizeCallbacks = [];
    const createdCanvases = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement(tagName) {
            assert.equal(tagName, "canvas");
            const createdCanvas = new FakeCanvas();
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        constructor(callback) {
            resizeCallbacks.push(callback);
        }

        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

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
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
});

test("score-only canvas owns independent resize, scene cache, and disposal state", () => {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];
    const observers = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement() {
            const createdCanvas = new FakeCanvas();
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observed = [];
        isDisconnected = false;

        constructor() {
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
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
});

test("grand staff sizes signature glyphs to their requested heights", () => {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement() {
            const createdCanvas = new FakeCanvas();
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

    const canvas = new FakeCanvas();
    try {
        initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
        render(canvas, {
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

        const scoreContext = createdCanvases[0].context;
        assert.equal(scoreContext.measureTextCalls, 4);
        assert.equal(scoreContext.fillTextCalls[0].args[0], "♯");
        assert.ok(Math.abs(Number.parseFloat(scoreContext.fillTextCalls[0].font) - 20.4) < 1e-9);
        assert.equal(scoreContext.fillTextCalls[1].args[0], "4");
        assert.ok(Math.abs(Number.parseFloat(scoreContext.fillTextCalls[1].font) - 20.4) < 1e-9);
    } finally {
        dispose(canvas);
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
});

test("grand staff draws score beams", () => {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement() {
            const createdCanvas = new FakeCanvas();
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

    const canvas = new FakeCanvas();
    try {
        initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
        render(canvas, {
            kind: 0,
            lines: [],
            glyphs: [],
            notes: [],
            beams: [{ x0: -0.5, y0: 0.2, x1: 0.5, y1: 0.3, count: 1, stemDirection: 0 }],
            shouldClipNotesAtClefs: false,
        });

        assert.equal(createdCanvases[0].context.strokeCalls, 1);
    } finally {
        dispose(canvas);
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
});

test("grand staff draws compact angled noteheads", () => {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement() {
            const createdCanvas = new FakeCanvas();
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

    const canvas = new FakeCanvas();
    try {
        initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
        render(canvas, {
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

        const ellipse = createdCanvases[0].context.ellipseCalls[0];
        assert.equal(ellipse[2], 6.6);
        assert.equal(ellipse[3], 4.4);
        assert.equal(ellipse[4], -Math.PI / 8);
    } finally {
        dispose(canvas);
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
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

test("grand staff keeps ledger lines compact on wide canvases", () => {
    const originalWindow = globalThis.window;
    const originalDocument = globalThis.document;
    const originalResizeObserver = globalThis.ResizeObserver;
    const originalRequestAnimationFrame = globalThis.requestAnimationFrame;
    const originalCancelAnimationFrame = globalThis.cancelAnimationFrame;
    const createdCanvases = [];

    globalThis.window = { devicePixelRatio: 1 };
    globalThis.document = {
        createElement() {
            const createdCanvas = new FakeCanvas();
            createdCanvases.push(createdCanvas);
            return createdCanvas;
        },
    };
    globalThis.ResizeObserver = class {
        observe() { }
        disconnect() { }
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => { };

    const canvas = new FakeCanvas();
    canvas.clientWidth = 1280;
    try {
        initialize(canvas, new FakeCanvas(), new FakeCanvas(), { spectrumVisibleBinCount: 32 });
        render(canvas, {
            kind: 0,
            lines: [
                { x0: -0.8, y0: 0.1, x1: 0.8, y1: 0.1, kind: 0 },
                { x0: -0.8, y0: 0, x1: 0.8, y1: 0, kind: 0 },
                { x0: -0.065, y0: -0.1, x1: 0.065, y1: -0.1, kind: 1 },
            ],
            glyphs: [],
            notes: [],
            beams: [],
            shouldClipNotesAtClefs: false,
        });

        const [firstStaffLine, secondStaffLine, ledgerLine] = createdCanvases[0].context.lineSegments;
        const staffSpace = Math.abs(firstStaffLine.y - secondStaffLine.y);
        const ledgerLineLength = Math.abs(ledgerLine.x1 - ledgerLine.x);
        assert.ok(ledgerLineLength <= staffSpace * 2);
    } finally {
        dispose(canvas);
        globalThis.window = originalWindow;
        globalThis.document = originalDocument;
        globalThis.ResizeObserver = originalResizeObserver;
        globalThis.requestAnimationFrame = originalRequestAnimationFrame;
        globalThis.cancelAnimationFrame = originalCancelAnimationFrame;
    }
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
