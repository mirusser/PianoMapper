import assert from "node:assert/strict";
import { stat } from "node:fs/promises";
import test from "node:test";

import {
    getPianoSamplesForVelocity,
    selectPianoSample,
} from "../../PianoMapper.Web/wwwroot/js/piano-samples.js";

test("piano sample manifest exposes one sampled note set for each velocity layer", () => {
    assert.deepEqual(
        [1, 32, 64, 96, 127].map(velocity => getPianoSamplesForVelocity(velocity)[0].velocityLayer),
        [1, 5, 10, 15, 15]);
    assert.equal(getPianoSamplesForVelocity(80).length, 30);
});

test("piano sample selection uses an exact recorded note without transposition", () => {
    const sample = selectPianoSample(440, 80);

    assert.equal(sample.fileName, "A4v10.mp3");
    assert.equal(sample.sampleMidi, 69);
    assert.equal(sample.playbackRate, 1);
});

test("piano sample selection transposes the nearest recorded note", () => {
    const sample = selectPianoSample(466.1637615, 110);

    assert.equal(sample.fileName, "A4v15.mp3");
    assert.equal(sample.sampleMidi, 69);
    assert.ok(Math.abs(sample.playbackRate - Math.pow(2, 1 / 12)) < 0.000001);
});

test("piano sample manifest references complete local velocity layers", async () => {
    const sampleDirectory = new URL(
        "../../PianoMapper.Web/wwwroot/audio/piano/salamander/",
        import.meta.url);
    const samples = [1, 32, 64, 96]
        .flatMap(getPianoSamplesForVelocity);

    assert.equal(samples.length, 120);
    await Promise.all(samples.map(async sample => {
        const sampleFile = await stat(new URL(sample.fileName, sampleDirectory));
        assert.ok(sampleFile.size > 1000, `${sample.fileName} is empty`);
    }));
});
