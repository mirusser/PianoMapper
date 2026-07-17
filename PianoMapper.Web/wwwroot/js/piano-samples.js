export const defaultPianoVelocity = 80;

const velocityLayers = [1, 5, 10, 15];
const sampledMidiNotes = [
    21, 24, 27, 30, 33, 36, 39, 42, 45, 48,
    51, 54, 57, 60, 63, 66, 69, 72, 75, 78,
    81, 84, 87, 90, 93, 96, 99, 102, 105, 108,
];
const noteNames = ["C", "Cs", "D", "Ds", "E", "F", "Fs", "G", "Gs", "A", "As", "B"];

export function getPianoSamplesForVelocity(velocity) {
    const velocityLayer = getVelocityLayer(velocity);
    return sampledMidiNotes.map(sampleMidi => ({
        fileName: `${getSampleNoteName(sampleMidi)}v${velocityLayer}.mp3`,
        sampleMidi,
        velocityLayer,
    }));
}

export function selectPianoSample(frequency, velocity = defaultPianoVelocity) {
    if (!Number.isFinite(frequency) || frequency <= 0) {
        throw new RangeError("Piano sample frequency must be positive.");
    }

    const targetMidi = 69 + (12 * Math.log2(frequency / 440));
    const sampleMidi = sampledMidiNotes.reduce((closest, candidate) =>
        Math.abs(candidate - targetMidi) < Math.abs(closest - targetMidi)
            ? candidate
            : closest);
    const velocityLayer = getVelocityLayer(velocity);
    return {
        fileName: `${getSampleNoteName(sampleMidi)}v${velocityLayer}.mp3`,
        playbackRate: Math.pow(2, (targetMidi - sampleMidi) / 12),
        sampleMidi,
        velocityLayer,
    };
}

function getVelocityLayer(velocity) {
    const clampedVelocity = Math.min(127, Math.max(1, velocity));
    const layerIndex = Math.min(
        velocityLayers.length - 1,
        Math.floor((clampedVelocity * velocityLayers.length) / 128));
    return velocityLayers[layerIndex];
}

function getSampleNoteName(midi) {
    const noteName = noteNames[midi % 12];
    const octave = Math.floor(midi / 12) - 1;
    return `${noteName}${octave}`;
}
