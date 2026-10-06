import assert from "node:assert/strict";
import test from "node:test";

import {
    clear,
    dispose as disposeAudio,
    getActiveNoteCount,
    initialize,
    noteOff,
    noteOn,
} from "../../PianoMapper.Web/wwwroot/js/audio.js";

// Velocity 100 maps to piano sample layer 15 and velocity 20 to layer 1; initialize() preloads the default layer (10).
const slowVelocity = 100;
const slowLayerSuffix = "v15.mp3";
const fastVelocity = 20;
const releaseSeconds = 0.08;

test("a tap released while its piano samples are still loading sounds as a short note and is released", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const pending = noteOn("tap", 440, 2, null, slowVelocity);
        noteOff("tap", 2.1);
        context.currentTime = 2.4;
        layer.open();
        await pending;

        assert.equal(sources.length, 1);
        assert.deepEqual(sources[0].startTimes, [2.4]);
        assert.equal(sources[0].stopTimes.length, 1);
        // Late start plus the held 0.1 s plus the normal release.
        assertCloseTo(sources[0].stopTimes[0], 2.4 + 0.1 + releaseSeconds);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("a note released at the same instant it started while loading is still released", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const pending = noteOn("tap", 440, 2, null, slowVelocity);
        noteOff("tap", 2);
        context.currentTime = 2.4;
        layer.open();
        await pending;

        assert.equal(sources[0].stopTimes.length, 1);
        assertCloseTo(sources[0].stopTimes[0], 2.4 + releaseSeconds);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("a note that is still held when its piano samples finish loading keeps sounding until released", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const pending = noteOn("held", 440, 2, null, slowVelocity);
        context.currentTime = 2.4;
        layer.open();
        await pending;

        assert.equal(sources[0].stopTimes.length, 0);
        assert.equal(getActiveNoteCount(), 1);

        noteOff("held", 3);

        assert.equal(sources[0].stopTimes.length, 1);
        assertCloseTo(sources[0].stopTimes[0], 3 + releaseSeconds);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("a note released while another pitch is loading does not release the loading pitch", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const pending = noteOn("a", 440, 2, null, slowVelocity);
        noteOff("b", 2.1);
        context.currentTime = 2.4;
        layer.open();
        await pending;

        assert.equal(sources[0].stopTimes.length, 0);
        assert.equal(getActiveNoteCount(), 1);
    });
});

test("note on, note off, then note on again while loading releases only the first note", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const first = noteOn("key", 440, 2, null, slowVelocity);
        noteOff("key", 2.03);
        const second = noteOn("key", 440, 2.06, null, slowVelocity);
        context.currentTime = 2.3;
        layer.open();
        await Promise.all([first, second]);

        assert.equal(sources.length, 2);
        assert.equal(sources[0].stopTimes.length, 1);
        assertCloseTo(sources[0].stopTimes[0], 2.3 + 0.03 + releaseSeconds);
        assert.equal(sources[1].stopTimes.length, 0);
        assert.equal(getActiveNoteCount(), 1);

        noteOff("key", 2.8);

        assert.equal(sources[1].stopTimes.length, 1);
        assertCloseTo(sources[1].stopTimes[0], 2.8 + releaseSeconds);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("retriggering a pitch that is still loading releases the first note at the retrigger", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const first = noteOn("key", 440, 2, null, slowVelocity);
        const second = noteOn("key", 440, 2.05, null, slowVelocity);
        context.currentTime = 2.3;
        layer.open();
        await Promise.all([first, second]);

        assert.equal(sources.length, 2);
        assert.equal(sources[0].stopTimes.length, 1);
        assertCloseTo(sources[0].stopTimes[0], 2.3 + 0.05 + releaseSeconds);
        assert.equal(sources[1].stopTimes.length, 0);
        assert.equal(getActiveNoteCount(), 1);

        noteOff("key", 2.8);

        assert.equal(sources[1].stopTimes.length, 1);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("a released note that finishes loading after a newer note of the same pitch does not release the newer note", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const slowLayer = holdLayer(slowLayerSuffix);

        const first = noteOn("key", 440, 2, null, slowVelocity);
        noteOff("key", 2.03);
        context.currentTime = 2.1;
        await noteOn("key", 440, 2.06, null, fastVelocity);
        assert.equal(sources.length, 1);
        assert.equal(getActiveNoteCount(), 1);

        context.currentTime = 2.5;
        slowLayer.open();
        await first;

        assert.equal(sources.length, 2);
        assert.equal(sources[0].stopTimes.length, 0, "the newer note must keep sounding");
        assert.equal(sources[1].stopTimes.length, 1, "the late first note is released by its own note off");
        assertCloseTo(sources[1].stopTimes[0], 2.5 + 0.03 + releaseSeconds);
        assert.equal(getActiveNoteCount(), 1);

        noteOff("key", 3);

        assert.equal(sources[0].stopTimes.length, 1);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("clearing while a note is loading releases it when it is created", async () => {
    await withPianoAudio(async ({ context, sources, holdLayer }) => {
        const layer = holdLayer(slowLayerSuffix);

        const pending = noteOn("key", 440, 2, null, slowVelocity);
        clear(2.2);
        context.currentTime = 2.4;
        layer.open();
        await pending;

        assert.equal(sources.length, 1);
        assert.equal(sources[0].stopTimes.length, 1);
        assertCloseTo(sources[0].stopTimes[0], 2.4 + 0.2 + releaseSeconds);
        assert.equal(getActiveNoteCount(), 0);
    });
});

test("a failed sample load does not swallow a later note off for an earlier note of the same pitch", async () => {
    await withPianoAudio(async ({ sources, holdLayer }) => {
        await noteOn("key", 440, 2, null);
        assert.equal(getActiveNoteCount(), 1);

        const layer = holdLayer(slowLayerSuffix);
        const failing = noteOn("key", 440, 2.5, null, slowVelocity);
        layer.fail(new Error("offline"));
        await assert.rejects(failing, /offline/);

        noteOff("key", 3);

        assert.equal(sources.length, 1);
        assert.equal(sources[0].stopTimes.length, 1);
        assert.equal(getActiveNoteCount(), 0);
    });
});

function assertCloseTo(actual, expected) {
    assert.ok(Math.abs(actual - expected) < 1e-9, `expected ${actual} to equal ${expected}`);
}

// Runs the test body against a Web Audio fake that records every buffer source's start and stop times, with a piano
// sample fetch that a test can hold back per velocity layer to model a layer that is not loaded yet.
async function withPianoAudio(body) {
    const originalDocument = Object.getOwnPropertyDescriptor(globalThis, "document");
    const originalFetch = Object.getOwnPropertyDescriptor(globalThis, "fetch");
    const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
    const sources = [];
    const gates = new Map();
    let context;

    class FakeAudioContext {
        constructor() {
            this.currentTime = 2;
            this.destination = {};
            this.state = "running";
            context = this;
        }

        createAnalyser() {
            return { connect() {} };
        }

        createGain() {
            return {
                connect() {},
                disconnect() {},
                gain: {
                    value: 0,
                    cancelScheduledValues() {},
                    exponentialRampToValueAtTime() {},
                    setValueAtTime() {},
                },
            };
        }

        createBufferSource() {
            const source = {
                startTimes: [],
                stopTimes: [],
                playbackRate: { setValueAtTime() {} },
                connect() {},
                disconnect() {},
                start(when) {
                    this.startTimes.push(when);
                },
                stop(when) {
                    this.stopTimes.push(when);
                },
            };
            sources.push(source);
            return source;
        }

        close() {
            this.state = "closed";
            return Promise.resolve();
        }

        decodeAudioData() {
            return Promise.resolve({});
        }
    }

    Object.defineProperty(globalThis, "document", {
        configurable: true,
        value: { cookie: "", querySelector: () => null, querySelectorAll: () => [] },
    });
    Object.defineProperty(globalThis, "fetch", {
        configurable: true,
        value: async url => {
            const gate = [...gates].find(([suffix]) => String(url).endsWith(suffix))?.[1];
            await gate?.promise;
            return { ok: true, arrayBuffer: () => Promise.resolve(new ArrayBuffer(8)) };
        },
    });
    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            AudioContext: FakeAudioContext,
            clearInterval() {},
            clearTimeout() {},
            setInterval: () => 1,
            setTimeout: () => 1,
        },
    });

    try {
        await initialize();
        await body({
            get context() {
                return context;
            },
            sources,
            holdLayer(suffix) {
                const gate = {};
                gate.promise = new Promise((resolve, reject) => {
                    gate.resolve = resolve;
                    gate.reject = reject;
                });
                gates.set(suffix, gate);
                return {
                    open() {
                        gates.delete(suffix);
                        gate.resolve();
                    },
                    fail(error) {
                        gates.delete(suffix);
                        gate.reject(error);
                    },
                };
            },
        });
    } finally {
        await disposeAudio();
        restoreProperty("document", originalDocument);
        restoreProperty("fetch", originalFetch);
        restoreProperty("window", originalWindow);
    }
}

function restoreProperty(name, descriptor) {
    if (descriptor) {
        Object.defineProperty(globalThis, name, descriptor);
        return;
    }

    delete globalThis[name];
}
