import assert from "node:assert/strict";
import test from "node:test";

import {
    attach,
    dispose,
} from "../../PianoMapper.Web/wwwroot/js/piano-keyboard-input.js";

class FakeKeyElement {
    constructor(pitch) {
        this.dataset = { pitch };
    }

    closest(selector) {
        return selector === "[data-pitch]" ? this : null;
    }
}

class FakeNonKeyElement {
    closest() {
        return null;
    }
}

class FakeContainer {
    listeners = new Map();
    capturedPointerIds = [];

    addEventListener(name, listener) {
        this.listeners.set(name, listener);
    }

    removeEventListener(name) {
        this.listeners.delete(name);
    }

    setPointerCapture(pointerId) {
        this.capturedPointerIds.push(pointerId);
    }

    dispatch(name, event) {
        this.listeners.get(name)?.(event);
    }
}

class FakeDotNetReference {
    calls = [];

    invokeMethodAsync(methodName, argument) {
        this.calls.push({ methodName, argument });
        return Promise.resolve();
    }
}

function createEvent(overrides) {
    return { pointerId: 1, clientX: 0, clientY: 0, timeStamp: 0, preventDefault() { }, ...overrides };
}

test("pointerdown on a key starts a note and captures the pointer", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    attach(container, dotNetReference);
    const keyElement = new FakeKeyElement("C4");

    container.dispatch("pointerdown", createEvent({ pointerId: 5, target: keyElement, timeStamp: 100 }));

    assert.deepEqual(container.capturedPointerIds, [5]);
    assert.equal(dotNetReference.calls.length, 1);
    assert.equal(dotNetReference.calls[0].methodName, "HandlePointerNoteOnAsync");
    assert.equal(dotNetReference.calls[0].argument.pointerId, "5");
    assert.equal(dotNetReference.calls[0].argument.pitch, "C4");
    assert.equal(dotNetReference.calls[0].argument.eventTimestampMilliseconds, 100);
    dispose();
});

test("pointerdown outside any key does nothing", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    attach(container, dotNetReference);

    container.dispatch("pointerdown", createEvent({ target: new FakeNonKeyElement() }));

    assert.equal(dotNetReference.calls.length, 0);
    assert.deepEqual(container.capturedPointerIds, []);
    dispose();
});

test("pointerup releases a started note exactly once", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    attach(container, dotNetReference);
    container.dispatch("pointerdown", createEvent({ pointerId: 1, target: new FakeKeyElement("C4") }));

    container.dispatch("pointerup", createEvent({ pointerId: 1, timeStamp: 200 }));

    assert.equal(dotNetReference.calls.length, 2);
    assert.equal(dotNetReference.calls[1].methodName, "HandlePointerNoteOffAsync");
    assert.equal(dotNetReference.calls[1].argument.pointerId, "1");
    assert.equal(dotNetReference.calls[1].argument.eventTimestampMilliseconds, 200);
    dispose();
});

test("pointercancel and lostpointercapture also release a started note", () => {
    for (const endEventName of ["pointercancel", "lostpointercapture"]) {
        const container = new FakeContainer();
        const dotNetReference = new FakeDotNetReference();
        attach(container, dotNetReference);
        container.dispatch("pointerdown", createEvent({ pointerId: 1, target: new FakeKeyElement("C4") }));

        container.dispatch(endEventName, createEvent({ pointerId: 1 }));

        assert.equal(
            dotNetReference.calls.filter(call => call.methodName === "HandlePointerNoteOffAsync").length,
            1,
            `${endEventName} should release the held note`);
        dispose();
    }
});

test("releasing an untracked pointer does nothing", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    attach(container, dotNetReference);

    container.dispatch("pointerup", createEvent({ pointerId: 99 }));

    assert.equal(dotNetReference.calls.length, 0);
    dispose();
});

test("gliding to a different key while pressed releases the old note and starts the new one", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    const firstKey = new FakeKeyElement("C4");
    const secondKey = new FakeKeyElement("D4");
    const originalDocument = globalThis.document;
    globalThis.document = { elementFromPoint: () => secondKey };
    attach(container, dotNetReference);
    container.dispatch("pointerdown", createEvent({ pointerId: 1, target: firstKey, timeStamp: 0 }));

    container.dispatch("pointermove", createEvent({ pointerId: 1, clientX: 10, clientY: 0, timeStamp: 50 }));

    assert.equal(dotNetReference.calls.length, 3);
    assert.equal(dotNetReference.calls[1].methodName, "HandlePointerNoteOffAsync");
    assert.equal(dotNetReference.calls[2].methodName, "HandlePointerNoteOnAsync");
    assert.equal(dotNetReference.calls[2].argument.pitch, "D4");
    dispose();
    if (originalDocument === undefined) {
        delete globalThis.document;
    } else {
        globalThis.document = originalDocument;
    }
});

test("moving within the same key does not retrigger the note", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    const keyElement = new FakeKeyElement("C4");
    const originalDocument = globalThis.document;
    globalThis.document = { elementFromPoint: () => keyElement };
    attach(container, dotNetReference);
    container.dispatch("pointerdown", createEvent({ pointerId: 1, target: keyElement }));

    container.dispatch("pointermove", createEvent({ pointerId: 1, clientX: 1, clientY: 1 }));

    assert.equal(dotNetReference.calls.length, 1);
    dispose();
    if (originalDocument === undefined) {
        delete globalThis.document;
    } else {
        globalThis.document = originalDocument;
    }
});

test("two different pointers can hold two different pitches simultaneously", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    attach(container, dotNetReference);

    container.dispatch("pointerdown", createEvent({ pointerId: 1, target: new FakeKeyElement("C4") }));
    container.dispatch("pointerdown", createEvent({ pointerId: 2, target: new FakeKeyElement("E4") }));

    assert.equal(dotNetReference.calls.length, 2);
    assert.equal(dotNetReference.calls[0].argument.pointerId, "1");
    assert.equal(dotNetReference.calls[1].argument.pointerId, "2");

    container.dispatch("pointerup", createEvent({ pointerId: 1 }));

    assert.equal(dotNetReference.calls.length, 3);
    assert.equal(dotNetReference.calls[2].argument.pointerId, "1");
    dispose();
});

test("dispose removes listeners so further dispatches do nothing", () => {
    const container = new FakeContainer();
    const dotNetReference = new FakeDotNetReference();
    attach(container, dotNetReference);
    container.dispatch("pointerdown", createEvent({ pointerId: 1, target: new FakeKeyElement("C4") }));
    const callCountBeforeDispose = dotNetReference.calls.length;

    dispose();
    container.dispatch("pointerup", createEvent({ pointerId: 1 }));

    assert.equal(container.listeners.size, 0);
    assert.equal(dotNetReference.calls.length, callCountBeforeDispose);
});
