import assert from "node:assert/strict";
import test from "node:test";

import {
    clearHistory,
    readHistoryJson,
    writeHistoryJson,
} from "../../PianoMapper.Web/wwwroot/js/sight-reading-history.js";

class FakeStorage {
    values = new Map();

    getItem(key) {
        return this.values.has(key) ? this.values.get(key) : null;
    }

    setItem(key, value) {
        this.values.set(key, value);
    }

    removeItem(key) {
        this.values.delete(key);
    }
}

class ThrowingStorage {
    getItem() {
        throw new Error("localStorage is disabled.");
    }

    setItem() {
        throw new Error("localStorage quota exceeded.");
    }

    removeItem() {
        throw new Error("localStorage is disabled.");
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

test("readHistoryJson returns null and reports available storage when nothing is stored", () => {
    withStorage(new FakeStorage(), () => {
        const result = readHistoryJson();

        assert.equal(result.json, null);
        assert.equal(result.isAvailable, true);
    });
});

test("readHistoryJson passes through a corrupt stored value without validating it", () => {
    const storage = new FakeStorage();
    storage.setItem("pianomapper-sight-reading-history-v1", "{ not valid json");
    withStorage(storage, () => {
        const result = readHistoryJson();

        assert.equal(result.json, "{ not valid json");
        assert.equal(result.isAvailable, true);
    });
});

test("readHistoryJson reports storage unavailable when localStorage access throws", () => {
    withStorage(new ThrowingStorage(), () => {
        const result = readHistoryJson();

        assert.equal(result.json, null);
        assert.equal(result.isAvailable, false);
    });
});

test("writeHistoryJson stores the given json under the namespaced key", () => {
    const storage = new FakeStorage();
    withStorage(storage, () => {
        const result = writeHistoryJson("[1,2,3]");

        assert.equal(result.isAvailable, true);
        assert.equal(storage.getItem("pianomapper-sight-reading-history-v1"), "[1,2,3]");
    });
});

test("writeHistoryJson reports storage unavailable when localStorage.setItem throws", () => {
    withStorage(new ThrowingStorage(), () => {
        const result = writeHistoryJson("[1,2,3]");

        assert.equal(result.isAvailable, false);
    });
});

test("clearHistory removes the namespaced key", () => {
    const storage = new FakeStorage();
    storage.setItem("pianomapper-sight-reading-history-v1", "[1]");
    withStorage(storage, () => {
        const result = clearHistory();

        assert.equal(result.isAvailable, true);
        assert.equal(storage.getItem("pianomapper-sight-reading-history-v1"), null);
    });
});

test("clearHistory reports storage unavailable when localStorage.removeItem throws", () => {
    withStorage(new ThrowingStorage(), () => {
        const result = clearHistory();

        assert.equal(result.isAvailable, false);
    });
});
