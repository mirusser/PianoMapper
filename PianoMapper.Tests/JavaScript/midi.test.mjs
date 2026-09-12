import assert from "node:assert/strict";
import test from "node:test";

import {
    clearMidiOutput,
    connect,
    connectIfPermitted,
    dispose,
    parseNoteMessage,
    sendMidiNoteOff,
    sendMidiNoteOn,
} from "../../PianoMapper.Web/wwwroot/js/midi.js";

test("parseNoteMessage note on preserves piano pitch velocity channel and timestamp", () => {
    const message = parseNoteMessage("roland", new Uint8Array([0x92, 60, 103]), 125.5);

    assert.deepEqual(message, {
        noteId: "midi:roland:2:60",
        midiNumber: 60,
        velocity: 103,
        isNoteOn: true,
        eventTimestampMilliseconds: 125.5,
    });
});

test("parseNoteMessage note off and zero-velocity note on both release notes", () => {
    const explicitNoteOff = parseNoteMessage("roland", new Uint8Array([0x80, 21, 64]), 10);
    const zeroVelocityNoteOn = parseNoteMessage("roland", new Uint8Array([0x90, 108, 0]), 20);

    assert.equal(explicitNoteOff.isNoteOn, false);
    assert.equal(zeroVelocityNoteOn.isNoteOn, false);
});

test("parseNoteMessage ignores non-note MIDI messages", () => {
    assert.equal(parseNoteMessage("roland", new Uint8Array([0xb0, 64, 127]), 10), null);
    assert.equal(parseNoteMessage("roland", new Uint8Array([0xf8]), 10), null);
});

test("connect attaches the Roland input and reports later disconnection", async () => {
    const input = {
        id: "roland",
        manufacturer: "Roland",
        name: "Roland Digital Piano",
        state: "connected",
        onmidimessage: null,
    };
    const access = {
        inputs: new Map([[input.id, input]]),
        outputs: new Map(),
        onstatechange: null,
    };
    const calls = [];
    const reference = {
        invokeMethodAsync(methodName, argument) {
            calls.push({ methodName, argument });
            return Promise.resolve();
        },
    };
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: { requestMIDIAccess: () => Promise.resolve(access) },
    });

    try {
        const status = await connect(reference);

        assert.deepEqual(status, {
            isSupported: true,
            isPermissionRequired: false,
            inputNames: ["Roland Digital Piano"],
            outputName: null,
        });
        input.onmidimessage({ data: new Uint8Array([0x90, 69, 88]), timeStamp: 250 });
        assert.equal(calls[0].methodName, "HandleMidiMessageAsync");
        assert.equal(calls[0].argument.midiNumber, 69);

        input.state = "disconnected";
        access.onstatechange();
        assert.deepEqual(calls[1], {
            methodName: "HandleMidiConnectionChangedAsync",
            argument: {
                isSupported: true,
                isPermissionRequired: false,
                inputNames: [],
                outputName: null,
            },
        });
        assert.equal(input.onmidimessage, null);
    } finally {
        dispose();
        restoreProperty("navigator", originalNavigator);
    }
});

test("connect reports browsers without Web MIDI support", async () => {
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: {},
    });

    try {
        const status = await connect({ invokeMethodAsync: () => Promise.resolve() });

        assert.deepEqual(status, {
            isSupported: false,
            isPermissionRequired: false,
            inputNames: [],
            outputName: null,
        });
    } finally {
        dispose();
        restoreProperty("navigator", originalNavigator);
    }
});

test("connectIfPermitted does not trigger the browser prompt before permission is granted", async () => {
    let requestCount = 0;
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: {
            requestMIDIAccess() {
                requestCount += 1;
                return Promise.reject(new Error("must not be called"));
            },
            permissions: {
                query: () => Promise.resolve({ state: "prompt" }),
            },
        },
    });

    try {
        const status = await connectIfPermitted({ invokeMethodAsync: () => Promise.resolve() });

        assert.equal(requestCount, 0);
        assert.deepEqual(status, {
            isSupported: true,
            isPermissionRequired: true,
            inputNames: [],
            outputName: null,
        });
    } finally {
        dispose();
        restoreProperty("navigator", originalNavigator);
    }
});

test("connectIfPermitted reconnects an already-authorized Roland input", async () => {
    const input = {
        id: "roland",
        name: "Roland Digital Piano",
        state: "connected",
        onmidimessage: null,
    };
    const access = {
        inputs: new Map([[input.id, input]]),
        outputs: new Map(),
        onstatechange: null,
    };
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: {
            requestMIDIAccess: () => Promise.resolve(access),
            permissions: {
                query: () => Promise.resolve({ state: "granted" }),
            },
        },
    });

    try {
        const status = await connectIfPermitted({ invokeMethodAsync: () => Promise.resolve() });

        assert.deepEqual(status, {
            isSupported: true,
            isPermissionRequired: false,
            inputNames: ["Roland Digital Piano"],
            outputName: null,
        });
        assert.equal(typeof input.onmidimessage, "function");
    } finally {
        dispose();
        restoreProperty("navigator", originalNavigator);
    }
});

test("connected Roland output receives notes on the selected-tone MIDI channel", async () => {
    const sent = [];
    const connectionChanges = [];
    let clearCount = 0;
    const throughOutput = createOutput("through", "Midi Through Port-0", "", sent);
    const rolandOutput = createOutput("roland", "Roland Digital Piano MIDI 1", "Roland", sent);
    rolandOutput.clear = () => {
        clearCount += 1;
    };
    const access = {
        inputs: new Map(),
        outputs: new Map([
            [throughOutput.id, throughOutput],
            [rolandOutput.id, rolandOutput],
        ]),
        onstatechange: null,
    };
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: { requestMIDIAccess: () => Promise.resolve(access) },
    });

    try {
        const status = await connect({
            invokeMethodAsync(methodName, argument) {
                connectionChanges.push({ methodName, argument });
                return Promise.resolve();
            },
        });

        assert.equal(status.outputName, "Roland Digital Piano MIDI 1");
        sendMidiNoteOn(60, 103, 250.5);
        sendMidiNoteOff(60, 275.5);
        clearMidiOutput();

        assert.deepEqual(sent, [
            { outputId: "roland", data: [0x93, 60, 103], timestamp: 250.5 },
            { outputId: "roland", data: [0x83, 60, 0], timestamp: 275.5 },
            { outputId: "roland", data: [0xb3, 123, 0], timestamp: undefined },
        ]);
        assert.equal(clearCount, 1);

        rolandOutput.state = "disconnected";
        access.onstatechange();
        assert.deepEqual(connectionChanges, [{
            methodName: "HandleMidiConnectionChangedAsync",
            argument: {
                isSupported: true,
                isPermissionRequired: false,
                inputNames: [],
                outputName: null,
            },
        }]);
    } finally {
        dispose();
        restoreProperty("navigator", originalNavigator);
    }
});

test("connect ignores the virtual MIDI Through ports when the FP-10 is absent", async () => {
    const throughInput = {
        id: "through-input",
        name: "Midi Through Port-0",
        state: "connected",
        onmidimessage: null,
    };
    const throughOutput = createOutput("through-output", "Midi Through Port-0", "", []);
    const access = {
        inputs: new Map([[throughInput.id, throughInput]]),
        outputs: new Map([[throughOutput.id, throughOutput]]),
        onstatechange: null,
    };
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: { requestMIDIAccess: () => Promise.resolve(access) },
    });

    try {
        const status = await connect({ invokeMethodAsync: () => Promise.resolve() });

        assert.deepEqual(status, {
            isSupported: true,
            isPermissionRequired: false,
            inputNames: [],
            outputName: null,
        });
        assert.equal(throughInput.onmidimessage, null);
    } finally {
        dispose();
        restoreProperty("navigator", originalNavigator);
    }
});

function createOutput(id, name, manufacturer, sent) {
    return {
        id,
        manufacturer,
        name,
        state: "connected",
        send(data, timestamp) {
            sent.push({ outputId: id, data: [...data], timestamp });
        },
        clear() {},
    };
}

function restoreProperty(name, descriptor) {
    if (descriptor) {
        Object.defineProperty(globalThis, name, descriptor);
        return;
    }

    delete globalThis[name];
}
