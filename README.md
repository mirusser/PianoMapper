# PianoMapper

PianoMapper captures piano performances and renders them as notation. The repository contains an OpenTK/OpenAL desktop app, a standalone Blazor WebAssembly app, and an ASP.NET Core host for image recognition. Both clients share music, score, timing, grading, and layout code from `PianoMapper.Core`.

## Features

- Browser USB MIDI input with velocity-sensitive note-on/note-off handling across an 88-key piano, plus MIDI output to the Roland FP-10 sound engine.
- Computer-key note input and octave selection in the legacy desktop client.
- Live grand staff and scrolling piano roll with clefs, ledger lines, accidentals, and note duration.
- A full 88-key browser piano from A0 through C8 that highlights live input and score-playback notes while they sound.
- Strict MusicXML (`.mxl`, `.musicxml`, or `.xml`) import for one part and up to two staves, including chords, ties, rests, dotted values, beam groups, backup/forward timing, preserved `up`/`down` stem direction, and piano fingering numbers.
- JPEG and PNG sheet-music import through an Audiveris optical music recognition (OMR) host, with recognized fingering numbers carried into the grand staff.
- Scheduled score playback, measure navigation, a tempo cursor, and random-measure playback. In the browser, imported scores keep the current five-measure grand-staff page and the next page visible together; playback and Practice alternate between those rows without replacing the row being played.
- Count-in practice with pitch/timing/duration verdicts and an accuracy summary.
- Optional browser metronome with accented downbeats, on-tempo feedback, and adjustable timing tolerance.
- Piano-style multi-harmonic synthesis, oscilloscope, and spectrum.
- Browser-local Web Audio, static publishing, and offline PWA startup after the first online load.

## Requirements

- .NET SDK 10.0.
- Desktop app: an OpenAL-capable audio device and a display.
- Browser app: a current desktop browser with WebAssembly, Web Audio, and Canvas 2D. USB MIDI features additionally require Web MIDI support, user permission, and a secure context such as HTTPS or localhost.
- Hosted browser launcher: Make and Bash. Automatic Audiveris setup also uses `curl`, `ar`, `sha256sum`, `tar`, and `unzstd`.
- Image import: `make` automatically installs a repo-local [Audiveris](https://audiveris.github.io/audiveris/) bundle on Linux x86_64. Other platforms require a separate Audiveris installation. MusicXML-only use does not require Audiveris.

## Run the desktop app

```bash
dotnet run --project PianoMapper/PianoMapper.csproj

# Load a MusicXML score at startup
dotnet run --project PianoMapper/PianoMapper.csproj -- --score path/to/piece.musicxml
```

## Run the browser app

```bash
# Recommended: hosted client with MusicXML and JPEG/PNG import
make

# Standalone client: MusicXML import
dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj

# Hosted client without make's Audiveris setup
dotnet run --project PianoMapper.Server/PianoMapper.Server.csproj
```

The hosted server also serves the Blazor client, so `make` starts the complete image-enabled browser app as one process; the standalone Web project does not need to run beside it. Open the URL printed by the development server. Select **Enable audio** before playing; browser autoplay policy requires that user action. Choose a `.mxl`, `.musicxml`, `.xml`, `.jpg`, `.jpeg`, or `.png` file with the on-page picker. The browser rejects files larger than 10 MiB before parsing or recognition.

Connect a USB MIDI piano to the computer before or while running the browser app. For a Roland FP-10, connect its square **USB COMPUTER** port to the computer with a data-capable USB cable; the rectangular **USB FOR UPDATE** port is not a MIDI connection. Select **Connect MIDI piano** and allow MIDI access when the browser asks. In Firefox, select **Remember this decision** if you want the piano to reconnect automatically on later visits. The Audio panel reports detected MIDI inputs and a compatible Roland output and provides a reconnect button.

The Audio panel has three sound sources. **Synth** and **PC piano** play through the computer. **FP-10** sends PianoMapper-generated test notes, random measures, and score playback to the piano on MIDI channel 4 so the FP-10's currently selected tone sounds through its speakers or headphones. Notes played physically on the FP-10 are not echoed back over MIDI because its local sound engine already sounds them. The metronome remains a computer-audio click.

On Linux x86_64, the first `make` downloads the official Audiveris 5.10.2 package (about 68 MiB), verifies its checksum, and extracts its bundled Java runtime under `.tools/`. Later runs reuse that local copy. An existing `audiveris` command on `PATH` takes precedence. Set `Omr__AudiverisExecutable` to use another launcher and skip the automatic install. `Omr__TimeoutSeconds` controls the recognition timeout and defaults to 180 seconds:

```bash
Omr__AudiverisExecutable=/opt/audiveris/bin/Audiveris make
```

Image recognition is approximate. Check imported pitches, rhythm, and fingerings against the source image before relying on playback or practice grading.

To open the app from a laptop on the same local network, run this command on the server:

```bash
dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj --urls http://0.0.0.0:5080
```

On the laptop, open `http://<server-LAN-IP>:5080` (for example, `http://192.168.0.74:5080` for `archie`). Allow incoming TCP port 5080 through the server firewall if the page does not load. Rendering and audio run on the laptop. The app and audio work over LAN HTTP, but browser MIDI access, PWA installation, and offline caching require HTTPS or localhost. To use a USB piano from the laptop without configuring HTTPS, use the [SSH tunnel option](docs/remote-access.md) so the app opens on a localhost URL.

The browser keyboard listener belongs to the focused play surface. It ignores form fields and does not register Space, Tab, Enter, Escape, Page Up/Down, or the arrow keys.

## Controls

| Function | Desktop | Browser |
|---|---|---|
| Notes | `A W S E D F R J U K I L ;` | Connected USB MIDI piano |
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

In the desktop app, changing octave while holding a note still releases the pitch that originally started. In the browser, an active practice session aborts on visibility loss and can be retried with T or the visible button. Disconnecting a MIDI device releases any notes it was holding in PianoMapper.

## Publish the browser PWA

```bash
dotnet publish PianoMapper.Web/PianoMapper.Web.csproj --configuration Release
```

Publish output is under `PianoMapper.Web/bin/Release/net10.0/publish/wwwroot/`. Serve that directory as static files over HTTPS. Configure the host to return `index.html` for client-side routes and preserve the `<base href="/">` path, or adjust the base path for a subdirectory deployment.

The first load needs network access so the service worker can cache the published assets. Close existing PianoMapper tabs after deploying a new version, then reopen the app so the new service worker can activate. MusicXML files are not bundled or cached from prior selections; select them again from local storage.

The standalone PWA has no process in which to run Audiveris, so it supports MusicXML import only. To deploy image import, publish and host the ASP.NET Core companion instead:

```bash
dotnet publish PianoMapper.Server/PianoMapper.Server.csproj --configuration Release
```

## Test

```bash
dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj
node --test PianoMapper.Tests/JavaScript/*.test.mjs
dotnet build PianoMapper.slnx --configuration Release
```

The manual browser checklist and current evidence are in [docs/browser-test-matrix.md](docs/browser-test-matrix.md).

## Current limits

- Multipart scores, tuplets, grace notes, tempo/time-signature changes, stem values `none`/`double`, and unsupported MusicXML semantics fail with a readable error. Repeat barlines are accepted, but playback remains linear.
- OMR output depends on scan quality and Audiveris recognition. The image importer corrects the narrow beginner-score case where an isolated fingering `3` is exported as an unbeamed quarter-note triplet; other recognition mistakes require correction in an external score editor.
- A touch piano, accounts, backend synchronization, and mobile-specific layout are outside the current browser release.
- Web MIDI input and FP-10 output depend on browser support, MIDI permission, and a secure context; the browser app reports when any of these prevent connection.
- The desktop app uses OpenAL PCM synthesis. The browser's Synth and PC piano sources use Web Audio with equivalent note lifecycle; the FP-10 source sends MIDI rather than browser audio.
- Desktop note-off stops its source immediately and can produce a small click. The browser applies a short release envelope.

For remote desktop and browser use, see [docs/remote-access.md](docs/remote-access.md).
