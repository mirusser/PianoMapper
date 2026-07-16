let audioContext;
let masterGain;
let analyser;
const activeNotes = new Map();
const scheduledScoreNotes = new Map();
const scheduledMetronomeClicks = new Set();
const schedulingDelaysMilliseconds = [];
let metronomeInterval;
let metronomeAnchorSeconds;
let metronomeSecondsPerBeat;
let metronomeBeatsPerMeasure;
let nextMetronomeBeatIndex;

const attackSeconds = 0.012;
const releaseSeconds = 0.08;
const metronomeLookaheadSeconds = 0.2;
const metronomeSchedulerIntervalMilliseconds = 25;
const metronomeClickDurationSeconds = 0.04;
const harmonics = [
    { multiplier: 1, gain: 0.72 },
    { multiplier: 2, gain: 0.2 },
    { multiplier: 3, gain: 0.08 },
];

export async function initialize() {
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
        analyser.smoothingTimeConstant = 0.8;
        masterGain.connect(analyser);
        analyser.connect(audioContext.destination);
    }

    return {
        performanceTimeMilliseconds: performance.now(),
        audioTimeSeconds: audioContext.currentTime,
    };
}

export function noteOn(noteId, frequency, startTimeSeconds, eventPerformanceTimeMilliseconds) {
    ensureReady();
    releaseNote(noteId, audioContext.currentTime);

    if (eventPerformanceTimeMilliseconds !== null) {
        schedulingDelaysMilliseconds.push(Math.max(0, performance.now() - eventPerformanceTimeMilliseconds));
        if (schedulingDelaysMilliseconds.length > 500) {
            schedulingDelaysMilliseconds.shift();
        }
    }

    const startTime = Math.max(startTimeSeconds, audioContext.currentTime);
    activeNotes.set(noteId, createNote(frequency, startTime));
}

export function getCurrentTime() {
    ensureInitialized();
    return audioContext.currentTime;
}

export function scheduleScore(events) {
    ensureReady();
    stopScore();

    for (const event of events) {
        const startTime = Math.max(event.startTimeSeconds, audioContext.currentTime);
        const note = createNote(event.frequency, startTime);
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

function createNote(frequency, startTime) {
    const envelope = audioContext.createGain();
    envelope.gain.setValueAtTime(0.0001, startTime);
    envelope.gain.exponentialRampToValueAtTime(1, startTime + attackSeconds);
    envelope.connect(masterGain);

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

    return { envelope, oscillators };
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
    const isRunning = audioContext.state === "running";
    const releaseTime = isRunning
        ? Math.max(releaseTimeSeconds, audioContext.currentTime)
        : audioContext.currentTime;
    const stopTime = isRunning ? releaseTime + releaseSeconds : audioContext.currentTime;
    note.envelope.gain.cancelScheduledValues(releaseTime);
    note.envelope.gain.setValueAtTime(Math.max(note.envelope.gain.value, 0.0001), releaseTime);
    if (isRunning) {
        note.envelope.gain.exponentialRampToValueAtTime(0.0001, stopTime);
    } else {
        note.envelope.gain.setValueAtTime(0.0001, stopTime);
    }

    for (const { oscillator } of note.oscillators) {
        oscillator.stop(stopTime);
    }

    window.setTimeout(() => {
        for (const { oscillator, harmonicGain } of note.oscillators) {
            oscillator.disconnect();
            harmonicGain.disconnect();
        }

        note.envelope.disconnect();
        onDisconnected?.();
    }, Math.max(0, (stopTime - audioContext.currentTime) * 1000) + 25);
}
