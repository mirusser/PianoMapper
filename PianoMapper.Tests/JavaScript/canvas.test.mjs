import assert from "node:assert/strict";
import test from "node:test";

import {
    dispose,
    initialize,
    render,
} from "../../PianoMapper.Web/wwwroot/js/canvas.js";

class FakeCanvasContext {
    strokeCalls = 0;
    drawImageCalls = 0;
    ellipseCalls = [];

    setTransform() { }
    clearRect() { }
    fillRect() { }
    strokeRect() { }
    fillText() { }
    save() { }
    translate() { }
    rotate() { }
    restore() { }
    beginPath() { }
    moveTo() { }
    lineTo() { }
    rect() { }
    clip() { }
    arc() { }
    quadraticCurveTo() { }
    fill() { }

    ellipse(...args) {
        this.ellipseCalls.push(args);
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
