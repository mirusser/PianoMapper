# PianoMapper

PianoMapper captures piano performances and renders them as notation. The repository contains an OpenTK/OpenAL desktop app, a standalone Blazor WebAssembly app, and an ASP.NET Core host for image recognition. Both clients share music, score, timing, grading, and layout code from `PianoMapper.Core`.

## Features

- Browser USB MIDI input with velocity-sensitive note-on/note-off handling across an 88-key piano, plus MIDI output to the Roland FP-10 sound engine.
- Computer-key note input and octave selection in the desktop client. The browser app has an equivalent, opt-in toggle that maps a typing-piano-style row of keys to notes, without requiring MIDI.
- Live grand staff and scrolling piano roll with clefs, ledger lines, accidentals, and note duration.
- A full 88-key browser piano from A0 through C8, playable by mouse, touch (including multitouch), or MIDI, that highlights live input and score-playback notes while they sound. The optional **Highlight next keys on the piano** setting (under Notation, off by default) also marks the next notes to play in amber during count-in practice and idle score checking; generated note-reading exercises hide it unless you tick the exercise's own **Highlight next keys on the piano** under While playing, because it reveals the answer.
- Strict MusicXML (`.mxl`, `.musicxml`, or `.xml`) import for one part and up to two staves, including chords, ties, rests, dotted values, beam groups, backup/forward timing, preserved `up`/`down` stem direction, tuplet ratios (with a rendered numeral for beamed triplets), and editable or automatically generated piano fingering numbers.
- Rendered `<notations>` marks on the grand staff: fermata, a beginner-piano articulation subset (staccato, tenuto, accent, staccatissimo), a trill-mark ornament, accidental marks, slurs (a thin arc distinct from a tie), arpeggiated/non-arpeggiated chord marks (a wavy line or bracket to the left of the chord), and glissando/slide lines connecting two noteheads. An articulation or ornament outside the supported subset fails import with a readable error instead of being silently dropped.
- MusicXML `<direction>`-level octave shifts in both directions and all standard sizes (8va/8vb, 15ma/15mb, and 22a/22b), preserving MusicXML's sounding `<pitch>` for playback and grading while rendering the written staff position with a dashed bracket and an `8`, `15`, or `22` numeral.
- JPEG and PNG sheet-music import through an Audiveris optical music recognition (OMR) host, with recognized fingering numbers carried into the grand staff.
- A shared PostgreSQL score library that saves the current imported score and loads it again without repeating MusicXML parsing or image recognition.
- Scheduled score playback, measure navigation, a tempo cursor, and random-measure playback. In the browser, imported scores keep a configurable grand-staff page size (a "Measures per page" control, defaulting to five measures, from one to twelve) and the next page visible together; playback and Practice alternate between those rows without replacing the row being played.
- Count-in practice with pitch/timing/duration verdicts and an accuracy summary.
- Selectable idle score checking for pitch/order only, written hold duration, or hold duration plus tempo-relative rhythm.
- Generated note-reading exercises: treble, bass, or grand-staff ranges (hands alternating, or **Hands together** for the five-note range); five-note, one-octave, ledger-line, G major, F major, D major, B♭ major, A minor (natural), accidentals (C♯, E♭, F♯, B♭), and triad-chord presets (I, IV and V in root position and both inversions); a **Pattern** of random, melodic, or same-interval notes where the range allows it; and fixed quarter notes or variable rhythm presets (Basic 4/4, Compound 6/8, Extended 4/4 with dotted notes, a whole note and an eighth rest, 3/4, 2/4, and Syncopated 4/4 with ties) drawn with their rests and ties. Five modes run in learning order: pitch only, rhythm only (tap the rhythm on any key), pitch + rhythm, pitch + hold, and pitch + hold + rhythm. Playable by MIDI, the on-screen piano, or the optional computer-key toggle — MIDI is never required.
- Timed exercises have an adjustable tempo (slow by default), a one-measure count-in, a click that keeps going through the exercise (on by default), a beat indicator, and live early/late feedback. **Wait for me** (the default) waits for the right key before moving on; **Play along** follows the clock, keeps moving, and grades missed and extra notes. Pitch, onset, and duration are graded separately, so the right key played late counts as a timing mistake, not a pitch mistake.
- Answers stay hidden while an exercise is active: note names, fingering, next-key highlighting, and the **Coach hints** that name a direction or the note after repeated wrong keys are each opt-in and off by default. Once the exercise completes, a **Mistakes to look at** list gives each mistake's bar and beat, the expected and played notes with the direction or interval between them, and its timing, alongside first-try accuracy, pitch and timing counts, elapsed time, and a **Retry missed notes** action. Review also draws a **review mark** on the staff: a halo ring around each note (one ring around a whole chord, or one hand of a hands-together prompt) that was not first-try clean, in either pacing — a solid orange ring for a timing mistake (early, late, released too soon or held too long), a thicker solid red ring when a wrong key was played first, and a dashed grey ring for a note that was never played in Play along. A clean note has no ring, a wrong key outranks a missed note, which outranks a timing mistake, and nothing is drawn while the exercise is active.
- Completed exercises are saved to browser-local history (degrading safely if local storage is unavailable). The history shapes the next exercise, favoring pitches (per staff) with lower recent first-try accuracy or slower responses, and feeds a **Progress** panel with recent sessions, weak spots and habits, speed, a trend, and a **Guided path** of levels that only recommends (a level is passed after three matching sessions at 90% pitch first try, and 80% on time for timed levels; nothing is locked). **Start recommended exercise** applies the next level, and **Drill my weak notes** generates an exercise from the weak notes of the current range.
- Optional browser metronome with accented downbeats, on-tempo feedback, and adjustable timing tolerance.
- Piano-style multi-harmonic synthesis, oscilloscope, and spectrum.
- Browser-local Web Audio, static publishing, and offline PWA startup after the first online load.

## Requirements

- .NET SDK 10.0.
- Desktop app: an OpenAL-capable audio device and a display.
- Browser app: a current desktop browser with WebAssembly, Web Audio, and Canvas 2D. USB MIDI features additionally require Web MIDI support, user permission, and a secure context such as HTTPS or localhost.
- Hosted browser launcher: Make, Bash, Docker, and the Docker Compose plugin. Automatic Audiveris setup also uses `curl`, `ar`, `sha256sum`, `tar`, and `unzstd`.
- Image import: `make` automatically installs a repo-local [Audiveris](https://audiveris.github.io/audiveris/) bundle on Linux x86_64. Other platforms require a separate Audiveris installation. MusicXML-only use does not require Audiveris.

## Run the desktop app

```bash
dotnet run --project PianoMapper/PianoMapper.csproj

# Load a MusicXML score at startup
dotnet run --project PianoMapper/PianoMapper.csproj -- --score path/to/piece.musicxml
```

## Run the browser app

```bash
# Recommended: hosted client with PostgreSQL, MusicXML, and JPEG/PNG import
make

# Standalone client: MusicXML import without saved scores
dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj

# Hosted client without make's PostgreSQL and Audiveris setup
docker compose up --detach --wait postgres
dotnet run --project PianoMapper.Server/PianoMapper.Server.csproj
```

The hosted server also serves the Blazor client, so `make` starts PostgreSQL and the complete image-enabled browser app. The standalone Web project does not need to run beside it, but it cannot use the server-backed saved-score library. Open the URL printed by the development server. Select **Enable audio** before playing; browser autoplay policy requires that user action. Choose a `.mxl`, `.musicxml`, `.xml`, `.jpg`, `.jpeg`, or `.png` file with the on-page picker. The browser rejects files larger than 10 MiB before parsing or recognition.

The development PostgreSQL container listens only on `localhost:5434` and keeps its data in the `pianomapper-postgres-data` Docker volume. `PianoMapper.Server` creates the single `scores` application table when it starts. To use an existing PostgreSQL server instead, set the connection string before starting the host; `make` skips the development container when this value is present:

```bash
ConnectionStrings__PianoMapper='Host=db.example;Database=pianomapper;Username=pianomapper;Password=secret' make
```

Imported files and recognized images both become the same PianoMapper score document. **Save score** creates a shared library entry, **Save changes** updates the loaded entry, and **Load** replaces the active score. **Delete** asks for confirmation and then permanently removes that library entry; if it was open, the score remains in memory and can be saved as a new entry. The original image or MusicXML bytes are not stored.

To correct a fingering, show the grand staff and enable **Edit fingering** under Notation. Select a note on either score row, or choose it from the accessible note list, then assign finger 1–5 or select **Remove** and choose **Right hand** or **Left hand**. The grand-staff label updates to `R` or `L` immediately. Edits update the current score immediately; use **Save score** or **Save changes** to persist them in the hosted score library.

Select **Regenerate all fingerings** to replace the finger number on every note with one deterministic, ergonomically optimized suggestion. Generation preserves each note's current right/left-hand assignment and does not preserve existing finger numbers. Confirming the operation changes only the open score; use **Save score** or **Save changes** afterward to persist the generated numbers.

Connect a USB MIDI piano to the computer before or while running the browser app. For a Roland FP-10, connect its square **USB COMPUTER** port to the computer with a data-capable USB cable; the rectangular **USB FOR UPDATE** port is not a MIDI connection. Select **Connect MIDI piano** and allow MIDI access when the browser asks. In Firefox, select **Remember this decision** if you want the piano to reconnect automatically on later visits. The Audio panel reports detected MIDI inputs and a compatible Roland output and provides a reconnect button.

The Audio panel has three sound sources. **Synth** and **PC piano** play through the computer. **FP-10** sends PianoMapper-generated test notes, random measures, and score playback to the piano on MIDI channel 4 so the FP-10's currently selected tone sounds through its speakers or headphones. Notes played physically on the FP-10 are not echoed back over MIDI because its local sound engine already sounds them. The metronome remains a computer-audio click.

On Linux x86_64, the first `make` downloads the official Audiveris 5.10.2 package (about 68 MiB), verifies its checksum, and extracts its bundled Java runtime under `.tools/`. Later runs reuse that local copy. An existing `audiveris` command on `PATH` takes precedence. Set `Omr__AudiverisExecutable` to use another launcher and skip the automatic install. `Omr__TimeoutSeconds` controls the recognition timeout and defaults to 180 seconds:

```bash
Omr__AudiverisExecutable=/opt/audiveris/bin/Audiveris make
```

Image recognition is approximate. Check imported pitches, rhythm, and fingerings against the source image before relying on playback or practice grading. Fingering mistakes can be corrected on the grand staff.

To open the app from a laptop on the same local network, run this command on the server:

```bash
dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj --urls http://0.0.0.0:5080
```

On the laptop, open `http://<server-LAN-IP>:5080` (for example, `http://192.168.0.74:5080` for `archie`). Allow incoming TCP port 5080 through the server firewall if the page does not load. Rendering and audio run on the laptop. The app and audio work over LAN HTTP, but browser MIDI access, PWA installation, and offline caching require HTTPS or localhost. To use a USB piano from the laptop without configuring HTTPS, use the [SSH tunnel option](docs/remote-access.md) so the app opens on a localhost URL.

The browser keyboard listener belongs to the focused play surface. It ignores form fields and does not register Space, Tab, Enter, Escape, Page Up/Down, or the arrow keys.

## Controls

| Function | Desktop | Browser |
|---|---|---|
| Notes | `A W S E D F R J U K I L ;` | Connected USB MIDI piano, mouse/touch on the on-screen 88-key piano, or the optional computer-key toggle (`Z S X D C V G B H N J M , L .`) |
| Clear notes | Space | `C` |
| Octave down/up | Arrow Down/Up | On-page notation-focus buttons |
| Select octave | `1` through `8` | On-page notation-focus selector |
| Toggle staff/roll | Tab | `V` |
| Play/restart score | `P` | `P` |
| Previous/next measure group | Page Up/Down | `[` / `]` |
| Start/retry practice | Enter | `T` |
| Abort practice | Escape or Space | `C` |
| Random measure | `M` | `M` |
| Exit | `Q` | Use the browser tab/window control |

In the desktop app, changing octave while holding a note still releases the pitch that originally started. In the browser, an active practice session aborts on visibility loss and can be retried with T or the visible button. Disconnecting a MIDI device, or losing tab focus or visibility, releases any notes held by MIDI, the on-screen piano, or the computer-key toggle.

The browser's computer-key toggle is off by default and its codes intentionally overlap a few existing shortcuts (`C`, `V`, `M`); when it's on, piano notes take priority on those codes over Clear notes, Toggle staff/roll, and Random measure. Its mapped octave follows the same notation-focus octave control the MIDI/on-screen piano uses.

## Publish the browser PWA

```bash
dotnet publish PianoMapper.Web/PianoMapper.Web.csproj --configuration Release
```

Publish output is under `PianoMapper.Web/bin/Release/net10.0/publish/wwwroot/`. Serve that directory as static files over HTTPS. Configure the host to return `index.html` for client-side routes and preserve the `<base href="/">` path, or adjust the base path for a subdirectory deployment.

The first load needs network access so the service worker can cache the published assets. Close existing PianoMapper tabs after deploying a new version, then reopen the app so the new service worker can activate. MusicXML files are not bundled or cached from prior selections; select them again from local storage.

The standalone PWA has no process in which to run Audiveris or connect to PostgreSQL, so it supports MusicXML import only and does not provide saved scores. To deploy image import and the saved-score library, publish and host the ASP.NET Core companion instead:

```bash
dotnet publish PianoMapper.Server/PianoMapper.Server.csproj --configuration Release
```

Set `ConnectionStrings__PianoMapper` for the production PostgreSQL database before starting the published server. The development connection string is loaded only in the Development environment.

## Test

```bash
dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj
node --test PianoMapper.Tests/JavaScript/*.test.mjs
dotnet build PianoMapper.slnx --configuration Release
```

The manual browser checklist and current evidence are in [docs/browser-test-matrix.md](docs/browser-test-matrix.md).

## Current limits

- Multipart scores, grace notes, tempo/time-signature changes, stem values `none`/`double`, and unsupported MusicXML semantics fail with a readable error. Repeat barlines are accepted, but playback remains linear.
- Tuplets import and play back at the correct duration for any `actual:normal` ratio, but only a beamed triplet (3:2) gets a rendered numeral on the grand staff today; other ratios and unbeamed tuplets import and sound correctly without a visual indicator.
- `<other-notation>`, MusicXML's arbitrary vendor-extension escape hatch (a `type` attribute plus free text, no fixed visual meaning), imports without error but is deliberately never rendered — inventing a generic visual for unknown vendor content would be guessing, not implementing a notation.
- OMR output depends on scan quality and Audiveris recognition. The image importer corrects the narrow beginner-score case where an isolated fingering `3` is exported as an unbeamed quarter-note triplet. Fingering mistakes can be corrected on the grand staff; pitch, rhythm, and other recognition mistakes still require an external score editor.
- The 88-key browser piano responds to touch and supports multitouch, but accounts, backend synchronization, and mobile-specific layout are outside the current browser release.
- Web MIDI input and FP-10 output depend on browser support, MIDI permission, and a secure context; the browser app reports when any of these prevent connection.
- Web MIDI sustain-pedal control changes are not interpreted; hold-duration checking ends a note when its key sends note-off.
- Timed exercises compare key presses with the audio clock and do not compensate for audio output latency, so on hardware with a large latency a player in sync with the heard click can be graded slightly late. Timing-offset calibration has not been built.
- Rests and ties are drawn only in generated note-reading exercises. In an imported score, rests affect spacing and timing but are not drawn, and tied notes show as separate noteheads without a tie curve.
- The desktop app uses OpenAL PCM synthesis. The browser's Synth and PC piano sources use Web Audio with equivalent note lifecycle; the FP-10 source sends MIDI rather than browser audio.
- Desktop note-off stops its source immediately and can produce a small click. The browser applies a short release envelope.

For remote desktop and browser use, see [docs/remote-access.md](docs/remote-access.md).
