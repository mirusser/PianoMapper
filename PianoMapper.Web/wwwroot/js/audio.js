import {
    defaultPianoVelocity,
    getPianoSamplesForVelocity,
    selectPianoSample,
} from "./piano-samples.js";
import {
    createSoundSourceCookie,
    defaultSoundSource,
    externalMidiSoundSource,
    isSoundSource,
    readSoundSourcePreference,
} from "./audio-preferences.js";
import {
    clearMidiOutput,
    hasMidiOutput,
    sendMidiNoteOff,
    sendMidiNoteOn,
} from "./midi.js";

let audioContext;
let masterGain;
let analyser;
let soundSource = defaultSoundSource;
const activeNotes = new Map();
const scheduledScoreNotes = new Map();
const scheduledMetronomeClicks = new Set();
const schedulingDelaysMilliseconds = [];
const pianoBuffers = new Map();
const pianoLayerLoads = new Map();
let metronomeInterval;
let metronomeAnchorSeconds;
let metronomeSecondsPerBeat;
let metronomeBeatsPerMeasure;
let nextMetronomeBeatIndex;

const attackSeconds = 0.012;
const releaseSeconds = 0.08;
const minimumEnvelopeGain = 0.0001;
const metronomeLookaheadSeconds = 0.2;
const metronomeSchedulerIntervalMilliseconds = 25;
const metronomeClickDurationSeconds = 0.04;
const harmonics = [
    { multiplier: 1, gain: 0.72 },
    { multiplier: 2, gain: 0.2 },
    { multiplier: 3, gain: 0.08 },
];
const pianoSampleBaseUrl = new URL("../audio/piano/salamander/", import.meta.url);

export async function initialize() {
    soundSource = readSoundSourcePreference(document.cookie);
    const AudioContextType = window.AudioContext ?? window.webkitAudioContext;
    if (!AudioContextType) {
        throw new Error("Web Audio is not supported by this browser.");
    }

    audioContext ??= new AudioContextType();
    if (audioContext.state === "suspended") {
        await audioContext.resume();
    }

    if (audioContext.state !== "running") {
        throw new Error(`Web Audio is ${audioContext.state}.`);
    }

    if (!masterGain) {
        masterGain = audioContext.createGain();
        masterGain.gain.value = 0.35;
    }

    if (!analyser) {
        analyser = audioContext.createAnalyser();
        analyser.fftSize = 2048;
        analyser.smoothingTimeConstant = 0.2;
        masterGain.connect(analyser);
        analyser.connect(audioContext.destination);
    }

    if (soundSource === "piano") {
        await ensurePianoSamplesLoaded(defaultPianoVelocity);
    } else if (soundSource === externalMidiSoundSource) {
        ensureMidiOutputAvailable();
    }

    return {
        performanceTimeMilliseconds: performance.now(),
        audioTimeSeconds: audioContext.currentTime,
    };
}

export async function noteOn(
    noteId,
    frequency,
    startTimeSeconds,
    eventPerformanceTimeMilliseconds,
    velocity = defaultPianoVelocity) {
    ensureReady();
    if (soundSource === "piano") {
        await ensurePianoSamplesLoaded(velocity);
    } else if (soundSource === externalMidiSoundSource) {
        ensureMidiOutputAvailable();
    }

    releaseNote(noteId, audioContext.currentTime);

    if (eventPerformanceTimeMilliseconds !== null) {
        schedulingDelaysMilliseconds.push(Math.max(0, performance.now() - eventPerformanceTimeMilliseconds));
        if (schedulingDelaysMilliseconds.length > 500) {
            schedulingDelaysMilliseconds.shift();
        }
    }

    const startTime = Math.max(startTimeSeconds, audioContext.currentTime);
    activeNotes.set(noteId, createNote(noteId, frequency, velocity, startTime));
}

export async function setSoundSource(source) {
    ensureReady();
    if (!isSoundSource(source)) {
        throw new RangeError(`Unknown sound source: ${source}`);
    }

    if (source === soundSource) {
        return;
    }

    if (source === "piano") {
        await ensurePianoSamplesLoaded(defaultPianoVelocity);
    } else if (source === externalMidiSoundSource) {
        ensureMidiOutputAvailable();
    }

    clear(audioContext.currentTime);
    stopScore();
    soundSource = source;
    document.cookie = createSoundSourceCookie(source);
}

export function getSoundSource() {
    return soundSource;
}

export function getCurrentTime() {
    ensureInitialized();
    return audioContext.currentTime;
}

export async function scheduleScore(events) {
    ensureReady();
    if (soundSource === "piano") {
        const velocities = [...new Set(events.map(event => event.velocity ?? defaultPianoVelocity))];
        await Promise.all(velocities.map(ensurePianoSamplesLoaded));
    } else if (soundSource === externalMidiSoundSource) {
        ensureMidiOutputAvailable();
    }

    stopScore();

    for (const event of events) {
        const startTime = Math.max(event.startTimeSeconds, audioContext.currentTime);
        const note = createNote(
            event.noteId,
            event.frequency,
            event.velocity ?? defaultPianoVelocity,
            startTime);
        scheduledScoreNotes.set(event.noteId, note);
        releaseNodes(note, startTime + event.durationSeconds, () => {
            if (scheduledScoreNotes.get(event.noteId) === note) {
                scheduledScoreNotes.delete(event.noteId);
            }
        });
    }
}

export function stopScore() {
    ensureInitialized();
    if (soundSource === externalMidiSoundSource) {
        if (scheduledScoreNotes.size === 0) {
            return;
        }

        scheduledScoreNotes.clear();
        clearMidiOutput();
        return;
    }

    const releaseTime = audioContext.currentTime;
    for (const [noteId, note] of scheduledScoreNotes) {
        scheduledScoreNotes.delete(noteId);
        releaseNodes(note, releaseTime);
    }
}

export function startMetronome(anchorSeconds, secondsPerBeat, beatsPerMeasure) {
    ensureReady();
    stopMetronome();

    metronomeAnchorSeconds = anchorSeconds;
    metronomeSecondsPerBeat = secondsPerBeat;
    metronomeBeatsPerMeasure = beatsPerMeasure;
    nextMetronomeBeatIndex = Math.max(
        0,
        Math.ceil((audioContext.currentTime - anchorSeconds) / secondsPerBeat));
    scheduleMetronomeClicks();
    metronomeInterval = window.setInterval(
        scheduleMetronomeClicks,
        metronomeSchedulerIntervalMilliseconds);
}

export function stopMetronome() {
    if (metronomeInterval !== undefined) {
        window.clearInterval(metronomeInterval);
        metronomeInterval = undefined;
    }

    for (const click of scheduledMetronomeClicks) {
        window.clearTimeout(click.pulseTimer);
        window.clearTimeout(click.pulseClearTimer);
        if (audioContext && audioContext.state !== "closed") {
            const stopTime = audioContext.currentTime;
            click.envelope.gain.cancelScheduledValues(stopTime);
            click.envelope.gain.setValueAtTime(0.0001, stopTime);
            click.oscillator.stop(stopTime);
        }
    }

    scheduledMetronomeClicks.clear();
    clearMetronomePulse();
}

function scheduleMetronomeClicks() {
    if (!audioContext || audioContext.state !== "running") {
        return;
    }

    const firstUnsoundedBeatIndex = Math.max(
        0,
        Math.ceil((audioContext.currentTime - metronomeAnchorSeconds) / metronomeSecondsPerBeat));
    nextMetronomeBeatIndex = Math.max(nextMetronomeBeatIndex, firstUnsoundedBeatIndex);
    const scheduleUntil = audioContext.currentTime + metronomeLookaheadSeconds;
    while (true) {
        const clickTime = metronomeAnchorSeconds + (nextMetronomeBeatIndex * metronomeSecondsPerBeat);
        if (clickTime > scheduleUntil) {
            return;
        }

        scheduleMetronomeClick(
            Math.max(clickTime, audioContext.currentTime),
            nextMetronomeBeatIndex % metronomeBeatsPerMeasure === 0);
        nextMetronomeBeatIndex++;
    }
}

function scheduleMetronomeClick(startTime, isDownbeat) {
    const oscillator = audioContext.createOscillator();
    const envelope = audioContext.createGain();
    const peakGain = isDownbeat ? 0.55 : 0.35;
    oscillator.type = "sine";
    oscillator.frequency.setValueAtTime(isDownbeat ? 1760 : 1320, startTime);
    envelope.gain.setValueAtTime(0.0001, startTime);
    envelope.gain.exponentialRampToValueAtTime(peakGain, startTime + 0.002);
    envelope.gain.exponentialRampToValueAtTime(0.0001, startTime + metronomeClickDurationSeconds);
    oscillator.connect(envelope);
    envelope.connect(masterGain);

    const click = { oscillator, envelope, pulseTimer: undefined, pulseClearTimer: undefined };
    scheduledMetronomeClicks.add(click);
    oscillator.onended = () => {
        scheduledMetronomeClicks.delete(click);
        oscillator.disconnect();
        envelope.disconnect();
    };
    click.pulseTimer = window.setTimeout(
        () => pulseMetronome(click, isDownbeat),
        Math.max(0, (startTime - audioContext.currentTime) * 1000));
    oscillator.start(startTime);
    oscillator.stop(startTime + metronomeClickDurationSeconds);
}

function pulseMetronome(click, isDownbeat) {
    const pulse = document.querySelector("[data-metronome-pulse]");
    if (!pulse) {
        return;
    }

    pulse.classList.toggle("metronome-pulse-downbeat", isDownbeat);
    pulse.classList.add("metronome-pulse-active");
    click.pulseClearTimer = window.setTimeout(
        () => clearMetronomePulse(),
        90);
}

function clearMetronomePulse() {
    const pulse = document.querySelector("[data-metronome-pulse]");
    pulse?.classList.remove("metronome-pulse-active", "metronome-pulse-downbeat");
}

function createNote(noteId, frequency, velocity, startTime) {
    if (soundSource === "piano") {
        return createPianoNote(frequency, velocity, startTime);
    }

    if (soundSource === externalMidiSoundSource) {
        return createExternalMidiNote(noteId, frequency, velocity, startTime);
    }

    return createSynthNote(frequency, startTime);
}

function createExternalMidiNote(noteId, frequency, velocity, startTime) {
    const wasSent = !noteId.startsWith("midi:");
    const midiNumber = frequencyToMidiNumber(frequency);
    if (wasSent) {
        sendMidiNoteOn(midiNumber, velocity, toPerformanceTimestamp(startTime));
    }

    return {
        isExternalMidi: true,
        midiNumber,
        startTime,
        wasSent,
    };
}

function createSynthNote(frequency, startTime) {
    const envelope = createNoteEnvelope(startTime);
    const oscillators = harmonics.map(harmonic => {
        const oscillator = audioContext.createOscillator();
        const harmonicGain = audioContext.createGain();
        oscillator.type = "sine";
        oscillator.frequency.setValueAtTime(frequency * harmonic.multiplier, startTime);
        harmonicGain.gain.value = harmonic.gain;
        oscillator.connect(harmonicGain);
        harmonicGain.connect(envelope);
        oscillator.start(startTime);
        return { oscillator, harmonicGain };
    });

    return {
        envelope,
        startTime,
        stop: stopTime => oscillators.forEach(({ oscillator }) => oscillator.stop(stopTime)),
        disconnect: () => {
            for (const { oscillator, harmonicGain } of oscillators) {
                oscillator.disconnect();
                harmonicGain.disconnect();
            }
        },
    };
}

function createPianoNote(frequency, velocity, startTime) {
    const sample = selectPianoSample(frequency, velocity);
    const buffer = pianoBuffers.get(getPianoBufferKey(sample));
    if (!buffer) {
        throw new Error(`Piano sample ${sample.fileName} is not loaded.`);
    }

    const envelope = createNoteEnvelope(startTime);
    const source = audioContext.createBufferSource();
    source.buffer = buffer;
    source.playbackRate.setValueAtTime(sample.playbackRate, startTime);
    source.connect(envelope);
    source.start(startTime);
    return {
        envelope,
        startTime,
        stop: stopTime => source.stop(stopTime),
        disconnect: () => source.disconnect(),
    };
}

function createNoteEnvelope(startTime) {
    const envelope = audioContext.createGain();
    envelope.gain.setValueAtTime(minimumEnvelopeGain, startTime);
    envelope.gain.exponentialRampToValueAtTime(1, startTime + attackSeconds);
    envelope.connect(masterGain);
    return envelope;
}

export function noteOff(noteId, releaseTimeSeconds) {
    ensureInitialized();
    releaseNote(noteId, releaseTimeSeconds);
}

export function clear(releaseTimeSeconds) {
    ensureInitialized();
    for (const noteId of [...activeNotes.keys()]) {
        releaseNote(noteId, releaseTimeSeconds);
    }
}

export function getSchedulingLatency() {
    const sorted = [...schedulingDelaysMilliseconds].sort((left, right) => left - right);
    if (sorted.length === 0) {
        return { sampleCount: 0, medianMilliseconds: 0, p95Milliseconds: 0 };
    }

    const medianIndex = Math.floor((sorted.length - 1) * 0.5);
    const p95Index = Math.ceil(sorted.length * 0.95) - 1;
    return {
        sampleCount: sorted.length,
        medianMilliseconds: sorted[medianIndex],
        p95Milliseconds: sorted[p95Index],
    };
}

export function getActiveNoteCount() {
    return activeNotes.size + scheduledScoreNotes.size;
}

export function getAnalyserNode() {
    return analyser;
}

export function isAudioActive() {
    return activeNotes.size > 0 || scheduledScoreNotes.size > 0;
}

export async function dispose() {
    if (!audioContext) {
        return;
    }

    stopMetronome();
    clear(audioContext.currentTime);
    stopScore();
    await audioContext.close();
    audioContext = undefined;
    masterGain = undefined;
    analyser = undefined;
    soundSource = readSoundSourcePreference(document.cookie);
    pianoBuffers.clear();
    pianoLayerLoads.clear();
}

function ensureReady() {
    ensureInitialized();
    if (audioContext.state !== "running") {
        throw new Error("Audio is not initialized.");
    }
}

function ensureInitialized() {
    if (!audioContext || !masterGain || audioContext.state === "closed") {
        throw new Error("Audio is not initialized.");
    }
}

function releaseNote(noteId, releaseTimeSeconds) {
    const note = activeNotes.get(noteId);
    if (!note) {
        return;
    }

    activeNotes.delete(noteId);
    releaseNodes(note, releaseTimeSeconds);
}

function releaseNodes(note, releaseTimeSeconds, onDisconnected) {
    if (note.isExternalMidi) {
        releaseExternalMidiNote(note, releaseTimeSeconds, onDisconnected);
        return;
    }

    const isRunning = audioContext.state === "running";
    const releaseTime = isRunning
        ? Math.max(releaseTimeSeconds, audioContext.currentTime)
        : audioContext.currentTime;
    const stopTime = isRunning ? releaseTime + releaseSeconds : audioContext.currentTime;
    const releaseGain = getEnvelopeGainAtTime(note, releaseTime);
    if (typeof note.envelope.gain.cancelAndHoldAtTime === "function") {
        note.envelope.gain.cancelAndHoldAtTime(releaseTime);
    } else {
        note.envelope.gain.cancelScheduledValues(releaseTime);
        note.envelope.gain.setValueAtTime(releaseGain, releaseTime);
    }
    if (isRunning) {
        note.envelope.gain.exponentialRampToValueAtTime(minimumEnvelopeGain, stopTime);
    } else {
        note.envelope.gain.setValueAtTime(minimumEnvelopeGain, stopTime);
    }

    note.stop(stopTime);

    window.setTimeout(() => {
        note.disconnect();
        note.envelope.disconnect();
        onDisconnected?.();
    }, Math.max(0, (stopTime - audioContext.currentTime) * 1000) + 25);
}

function releaseExternalMidiNote(note, releaseTimeSeconds, onDisconnected) {
    if (note.wasSent && hasMidiOutput()) {
        sendMidiNoteOff(note.midiNumber, toPerformanceTimestamp(releaseTimeSeconds));
    }

    window.setTimeout(
        () => onDisconnected?.(),
        Math.max(0, (releaseTimeSeconds - audioContext.currentTime) * 1000) + 25);
}

function frequencyToMidiNumber(frequency) {
    return Math.max(0, Math.min(127, Math.round(69 + (12 * Math.log2(frequency / 440)))));
}

function toPerformanceTimestamp(audioTimeSeconds) {
    return performance.now() + (Math.max(0, audioTimeSeconds - audioContext.currentTime) * 1000);
}

function ensureMidiOutputAvailable() {
    if (!hasMidiOutput()) {
        throw new Error("Connect the FP-10 MIDI output before selecting FP-10 sound.");
    }
}

async function ensurePianoSamplesLoaded(velocity) {
    const samples = getPianoSamplesForVelocity(velocity);
    const velocityLayer = samples[0].velocityLayer;
    let load = pianoLayerLoads.get(velocityLayer);
    if (!load) {
        load = loadPianoSamples(samples);
        pianoLayerLoads.set(velocityLayer, load);
    }

    try {
        await load;
    } catch (error) {
        pianoLayerLoads.delete(velocityLayer);
        throw error;
    }
}

async function loadPianoSamples(samples) {
    await Promise.all(samples.map(async sample => {
        const key = getPianoBufferKey(sample);
        if (pianoBuffers.has(key)) {
            return;
        }

        const response = await fetch(new URL(sample.fileName, pianoSampleBaseUrl));
        if (!response.ok) {
            throw new Error(`Could not load piano sample ${sample.fileName}: HTTP ${response.status}.`);
        }

        const encodedAudio = await response.arrayBuffer();
        const buffer = await audioContext.decodeAudioData(encodedAudio);
        pianoBuffers.set(key, buffer);
    }));
}

function getPianoBufferKey(sample) {
    return `${sample.sampleMidi}:${sample.velocityLayer}`;
}

function getEnvelopeGainAtTime(note, timeSeconds) {
    const attackEndTime = note.startTime + attackSeconds;
    if (timeSeconds <= note.startTime) {
        return minimumEnvelopeGain;
    }

    if (timeSeconds >= attackEndTime) {
        return 1;
    }

    const attackProgress = (timeSeconds - note.startTime) / attackSeconds;
    return minimumEnvelopeGain * Math.pow(1 / minimumEnvelopeGain, attackProgress);
}
