import assert from "node:assert/strict";
import test from "node:test";

import {
    attach,
    dispose,
} from "../../PianoMapper.Web/wwwroot/js/keyboard.js";

class FakeElement {
    listeners = new Map();

    addEventListener(name, listener) {
        this.listeners.set(name, listener);
    }

    removeEventListener(name) {
        this.listeners.delete(name);
    }

    contains() {
        return false;
    }
}

class FakeInputElement {
    constructor(type) {
        this.type = type;
    }
}

test("handled shortcuts from non-editable inputs reach the play surface", () => {
    const originalDocument = globalThis.document;
    const originalInputElement = globalThis.HTMLInputElement;
    const originalTextAreaElement = globalThis.HTMLTextAreaElement;
    const originalSelectElement = globalThis.HTMLSelectElement;
    globalThis.document = new FakeElement();
    globalThis.HTMLInputElement = FakeInputElement;
    globalThis.HTMLTextAreaElement = class { };
    globalThis.HTMLSelectElement = class { };

    try {
        for (const inputType of ["checkbox", "file"]) {
            const playSurface = new FakeElement();
            const calls = [];
            const dotNetReference = {
                invokeMethodAsync(methodName, argument) {
                    calls.push({ methodName, argument });
                    return Promise.resolve();
                },
            };
            attach(playSurface, dotNetReference, ["KeyC"]);
            let wasPrevented = false;

            playSurface.listeners.get("keydown")({
                target: new FakeInputElement(inputType),
                code: "KeyC",
                repeat: false,
                timeStamp: 125,
                preventDefault() {
                    wasPrevented = true;
                },
            });

            assert.equal(calls.length, 1, `${inputType} input should not swallow shortcuts`);
            assert.equal(calls[0].methodName, "HandleKeyDownAsync");
            assert.equal(calls[0].argument.code, "KeyC");
            assert.equal(wasPrevented, true);
            dispose();
        }
    } finally {
        dispose();
        restoreGlobal("document", originalDocument);
        restoreGlobal("HTMLInputElement", originalInputElement);
        restoreGlobal("HTMLTextAreaElement", originalTextAreaElement);
        restoreGlobal("HTMLSelectElement", originalSelectElement);
    }
});

function restoreGlobal(name, value) {
    if (value === undefined) {
        delete globalThis[name];
        return;
    }

    globalThis[name] = value;
}
