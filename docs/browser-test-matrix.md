# Browser Test Matrix

Test the published PWA over HTTPS. Browser extensions, power-saving modes, and audio-device routing can change the result, so record the browser version and device with each manual run.

## Current evidence

| Browser | Platform | Keyboard and audio | MusicXML, playback, practice | Resize and offline | Status |
|---|---|---|---|---|---|
| Headless Chromium 149 | Linux (`archie`) | Audio unlock, 13 note bindings, polyphony, release, blur cleanup, analyser data, reserved keys | Supported and malformed import, score schedule, navigation, practice start/abort/retry, random measure | Canvas batches and published offline reload | Automated pass, 2026-07-15 |
| Chrome stable | Primary laptop | Pending physical audio and latency check | Pending | Pending install check | Manual pass required |
| Edge stable | Desktop | Pending | Pending | Pending install check | Manual pass required |
| Firefox stable | Desktop | Pending | Pending | Pending offline check | Manual pass required |
| Safari stable | macOS | No test device selected | No test device selected | No test device selected | Access decision required |

## Manual pass

Use one supported MusicXML fixture and one malformed file.

1. Load the page over HTTPS. Confirm the startup message changes and Enable audio can recover after a blocked or failed attempt.
2. Focus the play surface. Hold a chord, release each key, switch views with V, resize the window, and confirm no note sticks.
3. Use Tab, Enter, Space, Escape, Page Up/Down, and the arrow keys for their browser-native behavior. Type in the file input and octave selector without triggering piano commands.
4. Load the supported score. Use the buttons and `[`/`]` to navigate, then play and restart it with P.
5. Start practice with T, abort with C, retry, and hide the tab during a second run. The hidden run must abort and release audio.
6. Play a random measure with M. Return to the loaded score and play it again.
7. Close the tab after the online load, go offline, reopen the installed or cached PWA, and confirm the shell starts. MusicXML fixtures must still come from user file selection.

Record console errors, audible glitches, stuck notes, focus loss, and controls without a visible focus ring as failures.

## Free Play measure-boundary ties

Run these checks on the live grand staff. Record the browser, viewport, sound source, time signature, and result for each row.

| Scenario | Action | Expected result | Result |
|---|---|---|---|
| Internal barline | In 4/4, press a note on the final beat of a measure and hold it into the next measure. | Two noteheads appear inside their measures with one tie between them. The sound continues without a second attack. | Pending |
| Five-measure rollover | Hold a note across the boundary after the fifth visible measure. | The next view starts with a continuation head and incoming tie stub inside the score area. No note element enters the clef or signature strip. | Pending |
| Exact boundary release | Release a note as the cursor reaches a barline. | The finished measure contains one notehead. No empty continuation head or extra tie appears. | Pending |
| Accidental | Hold a sharp or flat pitch across a barline. | The accidental appears at the attack and does not repeat on continuation heads. | Pending |
| Chord | Hold two or more pitches across a barline, then release their keys separately. | Each pitch receives its own fragments and ties. Each sound stops once on its key release. | Pending |
| Time signature | Repeat the internal and rollover checks in 3/4. | Fragment boundaries follow the 3-beat measures, and playback remains continuous. | Pending |
| Sound source | Repeat an internal crossing with piano, then synth. | Both sources sustain through the barline without a new attack. | Pending |
| Responsive layout | Repeat an internal crossing in a narrow viewport and a wide viewport. | Ties stay clipped to the score and keep the same staff-relative curve and stroke proportions. | Pending |
| Piano roll | Switch to the piano roll while holding a note across a grand-staff barline. | The performance still appears as one continuous piano-roll bar. | Pending |
