let attachedKeyboard;

export function attach(element, dotNetReference, handledCodes) {
    dispose();

    const handledCodeSet = new Set(handledCodes);
    const keyDown = event => {
        if (isEditableTarget(event.target)) {
            return;
        }

        if (!handledCodeSet.has(event.code)) {
            return;
        }

        event.preventDefault();
        invoke(dotNetReference, "HandleKeyDownAsync", createKeyboardEvent(event));
    };
    const keyUp = event => {
        if (isEditableTarget(event.target)) {
            return;
        }

        if (!handledCodeSet.has(event.code)) {
            return;
        }

        event.preventDefault();
        invoke(dotNetReference, "HandleKeyUpAsync", createKeyboardEvent(event));
    };
    const focusLost = event => {
        if (event?.relatedTarget && element.contains(event.relatedTarget)) {
            return;
        }

        const timestamp = performance.now();
        queueMicrotask(() => invoke(dotNetReference, "HandleFocusLostAsync", timestamp));
    };
    const visibilityChanged = () => {
        if (document.hidden) {
            focusLost();
        }
    };

    element.addEventListener("keydown", keyDown);
    element.addEventListener("keyup", keyUp);
    element.addEventListener("focusout", focusLost);
    document.addEventListener("visibilitychange", visibilityChanged);
    attachedKeyboard = { element, keyDown, keyUp, focusLost, visibilityChanged };
}

export function dispose() {
    if (!attachedKeyboard) {
        return;
    }

    const { element, keyDown, keyUp, focusLost, visibilityChanged } = attachedKeyboard;
    element.removeEventListener("keydown", keyDown);
    element.removeEventListener("keyup", keyUp);
    element.removeEventListener("focusout", focusLost);
    document.removeEventListener("visibilitychange", visibilityChanged);
    attachedKeyboard = undefined;
}

function createKeyboardEvent(event) {
    return {
        code: event.code,
        isRepeat: event.repeat,
        eventTimestampMilliseconds: event.timeStamp,
    };
}

function isEditableTarget(target) {
    return target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target instanceof HTMLSelectElement ||
        target?.isContentEditable === true;
}

function invoke(dotNetReference, methodName, argument) {
    dotNetReference.invokeMethodAsync(methodName, argument)
        .catch(error => console.error(`PianoMapper keyboard callback ${methodName} failed.`, error));
}
