const storageKey = "pianomapper-fingering-profile-v1";

export function readFingeringProfileJson() {
    try {
        return { json: localStorage.getItem(storageKey), isAvailable: true };
    } catch {
        return { json: null, isAvailable: false };
    }
}

export function writeFingeringProfileJson(json) {
    try {
        localStorage.setItem(storageKey, json);
        return { isAvailable: true };
    } catch {
        return { isAvailable: false };
    }
}
