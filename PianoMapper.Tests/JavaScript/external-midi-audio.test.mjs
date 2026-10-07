import assert from "node:assert/strict";
import test from "node:test";

import {
    dispose as disposeMidi,
    connect,
} from "../../PianoMapper.Web/wwwroot/js/midi.js";
import {
    clear,
    dispose as disposeAudio,
    getSoundSource,
    initialize,
    noteOff,
    noteOn,
    scheduleScore,
    setMetronomeSound,
    setSoundSource,
    startMetronome,
    stopMetronome,
    stopScore,
} from "../../PianoMapper.Web/wwwroot/js/audio.js";

test("audio initialization falls back to PC piano when the saved FP-10 output is unavailable", async () => {
    const originalDocument = Object.getOwnPropertyDescriptor(globalThis, "document");
    const originalFetch = Object.getOwnPropertyDescriptor(globalThis, "fetch");
    const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
    Object.defineProperty(globalThis, "document", {
        configurable: true,
        value: {
            cookie: "pianomapper-sound-source=external-midi",
            querySelector: () => null,
            querySelectorAll: () => [],
        },
    });
    Object.defineProperty(globalThis, "fetch", {
        configurable: true,
        value: () => Promise.resolve({
            ok: true,
            arrayBuffer: () => Promise.resolve(new ArrayBuffer(8)),
        }),
    });
    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            AudioContext: FakeAudioContext,
            clearInterval() {},
            setTimeout() {
                return 1;
            },
        },
    });

    try {
        await initialize();

        assert.equal(getSoundSource(), "piano");
        assert.match(document.cookie, /^pianomapper-sound-source=piano;/);
        await assert.rejects(
            setSoundSource("external-midi"),
            /Connect the FP-10 MIDI output/);
    } finally {
        await disposeAudio();
        restoreProperty("document", originalDocument);
        restoreProperty("fetch", originalFetch);
        restoreProperty("window", originalWindow);
    }
});

test("external MIDI sound sends generated notes to Roland without echoing its input", async () => {
    const sent = [];
    const output = {
        id: "roland-output",
        manufacturer: "Roland",
        name: "Roland Digital Piano MIDI 1",
        state: "connected",
        send(data, timestamp) {
            sent.push({ data: [...data], timestamp });
        },
        clear() {},
    };
    const access = {
        inputs: new Map(),
        outputs: new Map([[output.id, output]]),
        onstatechange: null,
    };
    const originalDocument = Object.getOwnPropertyDescriptor(globalThis, "document");
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
    Object.defineProperty(globalThis, "document", {
        configurable: true,
        value: {
            cookie: "pianomapper-sound-source=external-midi",
            querySelector: () => null,
            querySelectorAll: () => [],
        },
    });
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: { requestMIDIAccess: () => Promise.resolve(access) },
    });
    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            AudioContext: FakeAudioContext,
            clearInterval() {},
            setTimeout() {
                return 1;
            },
        },
    });

    try {
        await connect({ invokeMethodAsync: () => Promise.resolve() });
        await initialize();

        await noteOn("midi:roland-input:0:69", 440, 2, 100, 76);
        noteOff("midi:roland-input:0:69", 2.1);
        assert.deepEqual(sent, []);

        await noteOn("test-note", 440, 2.2, null, 96);
        noteOff("test-note", 2.5);

        assert.deepEqual(sent.map(message => message.data), [
            [0x93, 69, 96],
            [0x83, 69, 0],
        ]);
        assert.ok(sent.every(message => Number.isFinite(message.timestamp)));

        sent.length = 0;
        await scheduleScore([
            {
                noteId: "score-0",
                frequency: 261.625565,
                velocity: 84,
                startTimeSeconds: 2.25,
                durationSeconds: 0.5,
            },
        ]);
        await clear(2.1);

        assert.deepEqual(sent.map(message => message.data), [
            [0x93, 60, 84],
            [0x83, 60, 0],
        ]);

        stopScore();

        assert.deepEqual(sent.map(message => message.data), [
            [0x93, 60, 84],
            [0x83, 60, 0],
            [0xb3, 123, 0],
        ]);
    } finally {
        await disposeAudio();
        disposeMidi();
        restoreProperty("document", originalDocument);
        restoreProperty("navigator", originalNavigator);
        restoreProperty("window", originalWindow);
    }
});

test("metronome pulse marks every beat indicator on the page and clears them together", async () => {
    const originalDocument = Object.getOwnPropertyDescriptor(globalThis, "document");
    const originalFetch = Object.getOwnPropertyDescriptor(globalThis, "fetch");
    const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
    const indicators = [createFakeIndicator(), createFakeIndicator()];
    const beatLabels = [{ textContent: "Beat —" }, { textContent: "Beat —" }];
    const beatCurrent = [{ textContent: "—" }, { textContent: "—" }];
    const beatTotal = [{ textContent: "—" }, { textContent: "—" }];
    const timers = [];
    Object.defineProperty(globalThis, "document", {
        configurable: true,
        value: {
            cookie: "",
            querySelector: () => null,
            querySelectorAll: selector => selector === "[data-metronome-pulse]"
                ? indicators
                : selector === "[data-metronome-beat]"
                    ? beatLabels
                    : selector === "[data-metronome-beat-current]"
                        ? beatCurrent
                        : selector === "[data-metronome-beat-total]"
                            ? beatTotal
                            : [],
        },
    });
    Object.defineProperty(globalThis, "fetch", {
        configurable: true,
        value: () => Promise.resolve({
            ok: true,
            arrayBuffer: () => Promise.resolve(new ArrayBuffer(8)),
        }),
    });
    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            AudioContext: FakeAudioContext,
            clearInterval() {},
            clearTimeout() {},
            setInterval() {
                return 1;
            },
            setTimeout(callback) {
                timers.push(callback);
                return timers.length;
            },
        },
    });

    try {
        await initialize();
        startMetronome(2, 0.5, 4);

        assert.ok(timers.length > 0);
        timers[0]();

        for (const indicator of indicators) {
            assert.ok(indicator.classes.has("metronome-pulse-active"));
            assert.ok(indicator.classes.has("metronome-pulse-downbeat"));
        }
        assert.deepEqual(beatLabels.map(label => label.textContent), ["Beat 1 of 4", "Beat 1 of 4"]);
        assert.deepEqual(beatCurrent.map(label => label.textContent), [1, 1]);
        assert.deepEqual(beatTotal.map(label => label.textContent), [4, 4]);
        assert.ok(indicators.every(indicator => indicator.styles.get("--metronome-beat-progress") === "0.25"));

        stopMetronome();

        for (const indicator of indicators) {
            assert.equal(indicator.classes.size, 0);
        }
    } finally {
        stopMetronome();
        await disposeAudio();
        restoreProperty("document", originalDocument);
        restoreProperty("fetch", originalFetch);
        restoreProperty("window", originalWindow);
    }
});

test("metronome accents the downbeat strongest and each beat group start in 6/8 only", async () => {
    const originalDocument = Object.getOwnPropertyDescriptor(globalThis, "document");
    const originalFetch = Object.getOwnPropertyDescriptor(globalThis, "fetch");
    const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
    Object.defineProperty(globalThis, "document", {
        configurable: true,
        value: { cookie: "", querySelector: () => null, querySelectorAll: () => [] },
    });
    Object.defineProperty(globalThis, "fetch", {
        configurable: true,
        value: () => Promise.resolve({
            ok: true,
            arrayBuffer: () => Promise.resolve(new ArrayBuffer(8)),
        }),
    });
    const intervalCallbacks = [];
    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            AudioContext: FakeAudioContext,
            clearInterval() {},
            clearTimeout() {},
            setInterval(callback) {
                intervalCallbacks.push(callback);
                return intervalCallbacks.length;
            },
            setTimeout() {
                return 1;
            },
        },
    });

    try {
        await initialize();

        createdClicks.length = 0;
        startMetronome(2, 0.01, 6, [0, 3]);
        const compound = createdClicks.slice(0, 12).map(click => click.frequency);
        stopMetronome();

        createdClicks.length = 0;
        startMetronome(2, 0.01, 4, [0]);
        const simple = createdClicks.slice(0, 8).map(click => click.frequency);
        stopMetronome();

        const [downbeat, groupStart, ordinary] = [1760, 1540, 1320];
        assert.deepEqual(compound, [
            downbeat, ordinary, ordinary, groupStart, ordinary, ordinary,
            downbeat, ordinary, ordinary, groupStart, ordinary, ordinary,
        ]);
        assert.deepEqual(simple, [
            downbeat, ordinary, ordinary, ordinary,
            downbeat, ordinary, ordinary, ordinary,
        ]);

        createdClicks.length = 0;
        startMetronome(2, 0.01, 5, [0, 2], 0.6, "triangle");
        const irregular = createdClicks.slice(0, 10);
        stopMetronome();

        assert.deepEqual(irregular.map(click => click.frequency), [
            downbeat, ordinary, groupStart, ordinary, ordinary,
            downbeat, ordinary, groupStart, ordinary, ordinary,
        ]);
        assert.ok(irregular.every(click => click.type === "triangle"));
        assert.equal(irregular[0].peakGain, 0.33);

        createdClicks.length = 0;
        startMetronome(2.5, 0.5, 4);
        setMetronomeSound(0.3, "triangle");
        lastCreatedAudioContext.currentTime = 2.31;
        intervalCallbacks.at(-1)();
        assert.equal(createdClicks.length, 1);
        const [liveSoundClick] = createdClicks;
        stopMetronome();

        assert.equal(liveSoundClick.type, "triangle");
        assert.equal(liveSoundClick.peakGain, 0.165);
    } finally {
        stopMetronome();
        await disposeAudio();
        restoreProperty("document", originalDocument);
        restoreProperty("fetch", originalFetch);
        restoreProperty("window", originalWindow);
    }
});

function createFakeIndicator() {
    const classes = new Set();
    const styles = new Map();
    return {
        classes,
        styles,
        classList: {
            add: (...names) => names.forEach(name => classes.add(name)),
            remove: (...names) => names.forEach(name => classes.delete(name)),
            toggle: (name, force) => (force ? classes.add(name) : classes.delete(name)),
        },
        style: {
            setProperty: (name, value) => styles.set(name, value),
        },
    };
}

const createdClicks = [];
let lastCreatedAudioContext;

class FakeAudioContext {
    constructor() {
        this.currentTime = 2;
        this.destination = {};
        this.state = "running";
        lastCreatedAudioContext = this;
    }

    createAnalyser() {
        return {
            connect() {},
        };
    }

    createGain() {
        const click = createdClicks.at(-1);
        return {
            connect() {},
            disconnect() {},
            gain: {
                value: 0,
                cancelScheduledValues() {},
                exponentialRampToValueAtTime(value) {
                    if (click && value > 0.001) {
                        click.peakGain = value;
                    }
                },
                setValueAtTime() {},
            },
        };
    }

    createOscillator() {
        const click = { frequency: undefined, type: undefined, peakGain: undefined };
        createdClicks.push(click);
        return {
            connect() {},
            disconnect() {},
            frequency: {
                setValueAtTime(value) {
                    click.frequency = value;
                },
            },
            set type(value) {
                click.type = value;
            },
            start() {},
            stop() {},
        };
    }

    close() {
        this.state = "closed";
        return Promise.resolve();
    }

    decodeAudioData() {
        return Promise.resolve({});
    }
}

function restoreProperty(name, descriptor) {
    if (descriptor) {
        Object.defineProperty(globalThis, name, descriptor);
        return;
    }

    delete globalThis[name];
}
