import assert from "node:assert/strict";
import test from "node:test";

import {
    createSoundSourceCookie,
    readSoundSourcePreference,
} from "../../PianoMapper.Web/wwwroot/js/audio-preferences.js";

test("sound source preference defaults to piano", () => {
    assert.equal(readSoundSourcePreference(""), "piano");
    assert.equal(readSoundSourcePreference("pianomapper-sound-source=unknown"), "piano");
});

test("sound source preference restores a saved source", () => {
    assert.equal(
        readSoundSourcePreference("theme=dark; pianomapper-sound-source=synth; session=abc"),
        "synth");
    assert.equal(readSoundSourcePreference("pianomapper-sound-source=piano"), "piano");
    assert.equal(
        readSoundSourcePreference("pianomapper-sound-source=external-midi"),
        "external-midi");
});

test("sound source preference creates a persistent site cookie", () => {
    assert.equal(
        createSoundSourceCookie("synth"),
        "pianomapper-sound-source=synth; Max-Age=31536000; Path=/; SameSite=Lax");
});
