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
