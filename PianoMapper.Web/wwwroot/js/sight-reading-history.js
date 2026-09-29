const storageKey = "pianomapper-sight-reading-history-v1";

export function readHistoryJson() {
    try {
        return { json: localStorage.getItem(storageKey), isAvailable: true };
    } catch {
        return { json: null, isAvailable: false };
    }
}

export function writeHistoryJson(json) {
    try {
        localStorage.setItem(storageKey, json);
        return { isAvailable: true };
    } catch {
        return { isAvailable: false };
    }
}

export function clearHistory() {
    try {
        localStorage.removeItem(storageKey);
        return { isAvailable: true };
    } catch {
        return { isAvailable: false };
    }
}
