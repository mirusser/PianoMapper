import assert from "node:assert/strict";
import test from "node:test";

import {
    readFingeringProfileJson,
    writeFingeringProfileJson,
} from "../../PianoMapper.Web/wwwroot/js/fingering-profile.js";

class FakeStorage {
    values = new Map();

    getItem(key) {
        return this.values.has(key) ? this.values.get(key) : null;
    }

    setItem(key, value) {
        this.values.set(key, value);
    }
}

class ThrowingStorage {
    getItem() {
        throw new Error("localStorage is disabled.");
    }

    setItem() {
        throw new Error("localStorage quota exceeded.");
    }
}

function withStorage(storage, run) {
    const original = globalThis.localStorage;
    globalThis.localStorage = storage;
    try {
        run();
    } finally {
        if (original === undefined) {
            delete globalThis.localStorage;
        } else {
            globalThis.localStorage = original;
        }
    }
}

test("readFingeringProfileJson passes through a corrupt stored value without validating it", () => {
    const storage = new FakeStorage();
    storage.setItem("pianomapper-fingering-profile-v1", "{ not valid json");
    withStorage(storage, () => {
        const result = readFingeringProfileJson();

        assert.equal(result.json, "{ not valid json");
        assert.equal(result.isAvailable, true);
    });
});

test("readFingeringProfileJson reports storage unavailable when localStorage access throws", () => {
    withStorage(new ThrowingStorage(), () => {
        const result = readFingeringProfileJson();

        assert.equal(result.json, null);
        assert.equal(result.isAvailable, false);
    });
});

test("writeFingeringProfileJson stores the given json under the namespaced key", () => {
    const storage = new FakeStorage();
    withStorage(storage, () => {
        const result = writeFingeringProfileJson('{"rightMaximum":8}');

        assert.equal(result.isAvailable, true);
        assert.equal(storage.getItem("pianomapper-fingering-profile-v1"), '{"rightMaximum":8}');
    });
});

test("writeFingeringProfileJson reports storage unavailable when localStorage.setItem throws", () => {
    withStorage(new ThrowingStorage(), () => {
        const result = writeFingeringProfileJson('{"rightMaximum":8}');

        assert.equal(result.isAvailable, false);
    });
});
