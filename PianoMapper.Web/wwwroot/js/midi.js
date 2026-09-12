let midiAccess;
let dotNetReference;
const attachedInputs = new Map();
let selectedOutput;

const selectedToneMidiChannel = 4;
const selectedToneChannelIndex = selectedToneMidiChannel - 1;
const noteOnStatus = 0x90 | selectedToneChannelIndex;
const noteOffStatus = 0x80 | selectedToneChannelIndex;
const controlChangeStatus = 0xb0 | selectedToneChannelIndex;
const allNotesOffController = 123;

export async function connect(reference) {
    dispose();
    dotNetReference = reference;

    if (typeof navigator.requestMIDIAccess !== "function") {
        return createConnectionStatus(false, false);
    }

    midiAccess = await navigator.requestMIDIAccess();
    synchronizePorts();
    midiAccess.onstatechange = handleStateChange;
    return createConnectionStatus(true, false);
}

export async function connectIfPermitted(reference) {
    if (typeof navigator.requestMIDIAccess !== "function") {
        return createConnectionStatus(false, false);
    }

    if (typeof navigator.permissions?.query !== "function") {
        return createConnectionStatus(true, true);
    }

    try {
        const permission = await navigator.permissions.query({ name: "midi", sysex: false });
        if (permission.state !== "granted") {
            return createConnectionStatus(true, true);
        }
    } catch {
        return createConnectionStatus(true, true);
    }

    return connect(reference);
}

export function dispose() {
    if (midiAccess) {
        midiAccess.onstatechange = null;
    }

    for (const { input } of attachedInputs.values()) {
        input.onmidimessage = null;
    }

    clearMidiOutput();
    attachedInputs.clear();
    selectedOutput = undefined;
    midiAccess = undefined;
    dotNetReference = undefined;
}

function handleStateChange() {
    synchronizePorts();
    invoke("HandleMidiConnectionChangedAsync", createConnectionStatus(true, false));
}

function synchronizePorts() {
    synchronizeInputs();
    synchronizeOutput();
}

function synchronizeInputs() {
    const connectedInputIds = new Set();
    for (const input of midiAccess.inputs.values()) {
        if (input.state !== "connected" || isMidiThrough(input)) {
            continue;
        }

        connectedInputIds.add(input.id);
        const attached = attachedInputs.get(input.id);
        if (attached?.input === input) {
            continue;
        }

        if (attached) {
            attached.input.onmidimessage = null;
        }

        input.onmidimessage = event => handleMidiMessage(input, event);
        attachedInputs.set(input.id, { input });
    }

    for (const [inputId, { input }] of attachedInputs) {
        if (connectedInputIds.has(inputId)) {
            continue;
        }

        input.onmidimessage = null;
        attachedInputs.delete(inputId);
    }
}

function synchronizeOutput() {
    const connectedOutputs = [...(midiAccess.outputs?.values() ?? [])]
        .filter(output => output.state === "connected");
    const preferredOutput = connectedOutputs.find(isRolandPiano);

    if (selectedOutput !== preferredOutput && selectedOutput?.state === "connected") {
        silenceOutput(selectedOutput);
    }

    selectedOutput = preferredOutput;
}

export function sendMidiNoteOn(midiNumber, velocity, timestampMilliseconds) {
    sendToSelectedOutput([noteOnStatus, midiNumber, velocity], timestampMilliseconds);
}

export function sendMidiNoteOff(midiNumber, timestampMilliseconds) {
    sendToSelectedOutput([noteOffStatus, midiNumber, 0], timestampMilliseconds);
}

export function clearMidiOutput() {
    if (selectedOutput?.state === "connected") {
        silenceOutput(selectedOutput);
    }
}

export function hasMidiOutput() {
    return selectedOutput?.state === "connected";
}

export function getMidiOutputName() {
    return hasMidiOutput() ? getPortName(selectedOutput) : null;
}

function handleMidiMessage(input, event) {
    const message = parseNoteMessage(input.id, event.data, event.timeStamp);
    if (message) {
        invoke("HandleMidiMessageAsync", message);
    }
}

export function parseNoteMessage(inputId, data, eventTimestampMilliseconds) {
    if (!data || data.length < 3) {
        return null;
    }

    const status = data[0];
    const messageType = status & 0xf0;
    if (messageType !== 0x80 && messageType !== 0x90) {
        return null;
    }

    const midiNumber = data[1];
    const velocity = data[2];
    const channel = status & 0x0f;
    return {
        noteId: `midi:${inputId}:${channel}:${midiNumber}`,
        midiNumber,
        velocity,
        isNoteOn: messageType === 0x90 && velocity > 0,
        eventTimestampMilliseconds,
    };
}

function createConnectionStatus(isSupported, isPermissionRequired) {
    return {
        isSupported,
        isPermissionRequired,
        inputNames: [...attachedInputs.values()].map(({ input }) =>
            getPortName(input, "MIDI input")),
        outputName: getMidiOutputName(),
    };
}

function sendToSelectedOutput(data, timestampMilliseconds) {
    if (!hasMidiOutput()) {
        throw new Error("No MIDI output is connected.");
    }

    selectedOutput.send(data, timestampMilliseconds);
}

function silenceOutput(output) {
    output.clear();
    output.send([controlChangeStatus, allNotesOffController, 0]);
}

function isRolandPiano(output) {
    const identity = `${output.manufacturer ?? ""} ${output.name ?? ""}`;
    return /roland|fp-?10/i.test(identity);
}

function isMidiThrough(output) {
    return /midi through/i.test(getPortName(output));
}

function getPortName(port, fallback = "MIDI output") {
    return port.name || port.manufacturer || fallback;
}

function invoke(methodName, argument) {
    dotNetReference?.invokeMethodAsync(methodName, argument)
        .catch(error => console.error(`PianoMapper MIDI callback ${methodName} failed.`, error));
}
