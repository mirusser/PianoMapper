let attachedKeyboard;
const activePointers = new Map();

export function attach(container, dotNetReference) {
    dispose();

    const pointerDown = event => {
        const keyElement = event.target.closest("[data-pitch]");
        if (!keyElement) {
            return;
        }

        event.preventDefault();
        container.setPointerCapture(event.pointerId);
        startPointerNote(event.pointerId, keyElement, dotNetReference, event.timeStamp);
    };
    const pointerMove = event => {
        if (!activePointers.has(event.pointerId)) {
            return;
        }

        const keyElement = document.elementFromPoint(event.clientX, event.clientY)?.closest("[data-pitch]");
        const current = activePointers.get(event.pointerId);
        if (!keyElement || keyElement === current.element) {
            return;
        }

        stopPointerNote(event.pointerId, dotNetReference, event.timeStamp);
        startPointerNote(event.pointerId, keyElement, dotNetReference, event.timeStamp);
    };
    const pointerEnd = event => stopPointerNote(event.pointerId, dotNetReference, event.timeStamp);

    container.addEventListener("pointerdown", pointerDown);
    container.addEventListener("pointermove", pointerMove);
    container.addEventListener("pointerup", pointerEnd);
    container.addEventListener("pointercancel", pointerEnd);
    container.addEventListener("lostpointercapture", pointerEnd);
    attachedKeyboard = { container, pointerDown, pointerMove, pointerEnd };
}

export function dispose() {
    if (!attachedKeyboard) {
        return;
    }

    const { container, pointerDown, pointerMove, pointerEnd } = attachedKeyboard;
    container.removeEventListener("pointerdown", pointerDown);
    container.removeEventListener("pointermove", pointerMove);
    container.removeEventListener("pointerup", pointerEnd);
    container.removeEventListener("pointercancel", pointerEnd);
    container.removeEventListener("lostpointercapture", pointerEnd);
    activePointers.clear();
    attachedKeyboard = undefined;
}

function startPointerNote(pointerId, keyElement, dotNetReference, timeStamp) {
    const pitch = keyElement.dataset.pitch;
    activePointers.set(pointerId, { element: keyElement });
    invoke(dotNetReference, "HandlePointerNoteOnAsync", {
        pointerId: String(pointerId),
        pitch,
        eventTimestampMilliseconds: timeStamp,
    });
}

function stopPointerNote(pointerId, dotNetReference, timeStamp) {
    if (!activePointers.has(pointerId)) {
        return;
    }

    activePointers.delete(pointerId);
    invoke(dotNetReference, "HandlePointerNoteOffAsync", {
        pointerId: String(pointerId),
        eventTimestampMilliseconds: timeStamp,
    });
}

function invoke(dotNetReference, methodName, argument) {
    dotNetReference.invokeMethodAsync(methodName, argument)
        .catch(error => console.error(`PianoMapper piano keyboard callback ${methodName} failed.`, error));
}
