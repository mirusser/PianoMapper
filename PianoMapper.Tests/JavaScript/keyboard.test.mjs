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

test("keyup for a handled code invokes HandleKeyUpAsync and prevents default", () => {
    const originalDocument = globalThis.document;
    const originalInputElement = globalThis.HTMLInputElement;
    const originalTextAreaElement = globalThis.HTMLTextAreaElement;
    const originalSelectElement = globalThis.HTMLSelectElement;
    globalThis.document = new FakeElement();
    globalThis.HTMLInputElement = FakeInputElement;
    globalThis.HTMLTextAreaElement = class { };
    globalThis.HTMLSelectElement = class { };
    try {
        const playSurface = new FakeElement();
        const calls = [];
        const dotNetReference = {
            invokeMethodAsync(methodName, argument) {
                calls.push({ methodName, argument });
                return Promise.resolve();
            },
        };
        attach(playSurface, dotNetReference, ["KeyZ"]);
        let wasPrevented = false;

        playSurface.listeners.get("keyup")({
            target: {},
            code: "KeyZ",
            timeStamp: 250,
            preventDefault() {
                wasPrevented = true;
            },
        });

        assert.equal(calls.length, 1);
        assert.equal(calls[0].methodName, "HandleKeyUpAsync");
        assert.equal(calls[0].argument.code, "KeyZ");
        assert.equal(calls[0].argument.eventTimestampMilliseconds, 250);
        assert.equal(wasPrevented, true);
        dispose();
    } finally {
        restoreGlobal("document", originalDocument);
        restoreGlobal("HTMLInputElement", originalInputElement);
        restoreGlobal("HTMLTextAreaElement", originalTextAreaElement);
        restoreGlobal("HTMLSelectElement", originalSelectElement);
    }
});

test("keyup for an unhandled code is ignored", () => {
    const originalDocument = globalThis.document;
    const originalInputElement = globalThis.HTMLInputElement;
    const originalTextAreaElement = globalThis.HTMLTextAreaElement;
    const originalSelectElement = globalThis.HTMLSelectElement;
    globalThis.document = new FakeElement();
    globalThis.HTMLInputElement = FakeInputElement;
    globalThis.HTMLTextAreaElement = class { };
    globalThis.HTMLSelectElement = class { };
    try {
        const playSurface = new FakeElement();
        const calls = [];
        const dotNetReference = {
            invokeMethodAsync(methodName, argument) {
                calls.push({ methodName, argument });
                return Promise.resolve();
            },
        };
        attach(playSurface, dotNetReference, ["KeyZ"]);

        playSurface.listeners.get("keyup")({ target: {}, code: "KeyQ", timeStamp: 0, preventDefault() { } });

        assert.equal(calls.length, 0);
        dispose();
    } finally {
        restoreGlobal("document", originalDocument);
        restoreGlobal("HTMLInputElement", originalInputElement);
        restoreGlobal("HTMLTextAreaElement", originalTextAreaElement);
        restoreGlobal("HTMLSelectElement", originalSelectElement);
    }
});

test("keyup from an editable text target is ignored, but checkbox/file targets still reach the page", () => {
    const originalDocument = globalThis.document;
    const originalInputElement = globalThis.HTMLInputElement;
    const originalTextAreaElement = globalThis.HTMLTextAreaElement;
    const originalSelectElement = globalThis.HTMLSelectElement;
    globalThis.document = new FakeElement();
    globalThis.HTMLInputElement = FakeInputElement;
    globalThis.HTMLTextAreaElement = class { };
    globalThis.HTMLSelectElement = class { };

    try {
        const playSurface = new FakeElement();
        const calls = [];
        const dotNetReference = {
            invokeMethodAsync(methodName, argument) {
                calls.push({ methodName, argument });
                return Promise.resolve();
            },
        };
        attach(playSurface, dotNetReference, ["KeyZ"]);

        playSurface.listeners.get("keyup")({
            target: new FakeInputElement("text"),
            code: "KeyZ",
            timeStamp: 0,
            preventDefault() { },
        });
        assert.equal(calls.length, 0, "a text input should swallow the piano key-up");

        playSurface.listeners.get("keyup")({
            target: new FakeInputElement("checkbox"),
            code: "KeyZ",
            timeStamp: 0,
            preventDefault() { },
        });
        assert.equal(calls.length, 1, "a checkbox target should still allow the piano key-up through");
        dispose();
    } finally {
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
