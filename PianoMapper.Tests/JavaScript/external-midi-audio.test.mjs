import assert from "node:assert/strict";
import test from "node:test";

import {
    dispose as disposeMidi,
    connect,
} from "../../PianoMapper.Web/wwwroot/js/midi.js";
import {
    clear,
    dispose as disposeAudio,
    initialize,
    noteOff,
    noteOn,
    scheduleScore,
    stopScore,
} from "../../PianoMapper.Web/wwwroot/js/audio.js";

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

class FakeAudioContext {
    constructor() {
        this.currentTime = 2;
        this.destination = {};
        this.state = "running";
    }

    createAnalyser() {
        return {
            connect() {},
        };
    }

    createGain() {
        return {
            connect() {},
            gain: { value: 0 },
        };
    }

    close() {
        this.state = "closed";
        return Promise.resolve();
    }
}

function restoreProperty(name, descriptor) {
    if (descriptor) {
        Object.defineProperty(globalThis, name, descriptor);
        return;
    }

    delete globalThis[name];
}
