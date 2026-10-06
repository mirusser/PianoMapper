import assert from "node:assert/strict";
import test from "node:test";

import { canStartWithoutUserGesture } from "../../PianoMapper.Web/wwwroot/js/audio.js";

test("canStartWithoutUserGesture follows the browser's own audio-context autoplay policy where it reports one", () => {
    const cases = [
        { policy: "allowed", expected: true },
        { policy: "allowed-muted", expected: false },
        { policy: "disallowed", expected: false },
    ];

    for (const { policy, expected } of cases) {
        withNavigator({ getAutoplayPolicy: type => (type === "audiocontext" ? policy : "disallowed") }, () => {
            assert.equal(canStartWithoutUserGesture(), expected, `policy ${policy}`);
        });
    }
});

test("canStartWithoutUserGesture trusts the autoplay policy over user activation", () => {
    withNavigator({ getAutoplayPolicy: () => "allowed", userActivation: { hasBeenActive: false } }, () => {
        assert.equal(canStartWithoutUserGesture(), true);
    });
    withNavigator({ getAutoplayPolicy: () => "disallowed", userActivation: { hasBeenActive: true } }, () => {
        assert.equal(canStartWithoutUserGesture(), false);
    });
});

test("canStartWithoutUserGesture falls back to whether the page has had a user gesture", () => {
    withNavigator({ userActivation: { hasBeenActive: true } }, () => {
        assert.equal(canStartWithoutUserGesture(), true);
    });
    withNavigator({ userActivation: { hasBeenActive: false } }, () => {
        assert.equal(canStartWithoutUserGesture(), false);
    });
});

test("canStartWithoutUserGesture keeps trying when the browser offers no signal", () => {
    withNavigator({}, () => {
        assert.equal(canStartWithoutUserGesture(), true);
    });
});

test("canStartWithoutUserGesture does not create an audio context", () => {
    let created = 0;
    const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
    Object.defineProperty(globalThis, "window", {
        configurable: true,
        value: {
            AudioContext: class {
                constructor() {
                    created++;
                }
            },
        },
    });

    try {
        withNavigator({ userActivation: { hasBeenActive: false } }, () => canStartWithoutUserGesture());
        withNavigator({ userActivation: { hasBeenActive: true } }, () => canStartWithoutUserGesture());
    } finally {
        restoreProperty("window", originalWindow);
    }

    assert.equal(created, 0);
});

function withNavigator(value, body) {
    const original = Object.getOwnPropertyDescriptor(globalThis, "navigator");
    Object.defineProperty(globalThis, "navigator", { configurable: true, value });
    try {
        return body();
    } finally {
        restoreProperty("navigator", original);
    }
}

function restoreProperty(name, descriptor) {
    if (descriptor) {
        Object.defineProperty(globalThis, name, descriptor);
        return;
    }

    delete globalThis[name];
}
