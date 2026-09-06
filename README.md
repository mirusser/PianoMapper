# PianoMapper

PianoMapper maps a computer keyboard to piano notes and renders performances as notation. The repository contains an OpenTK/OpenAL desktop app, a standalone Blazor WebAssembly app, and an ASP.NET Core host for image recognition. Both clients share music, score, timing, grading, and layout code from `PianoMapper.Core`.

## Features

- Thirteen chromatic note keys with sustained note-on/note-off behavior and octave selection.
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
- Browser app: a current desktop browser with WebAssembly, Web Audio, and Canvas 2D. PWA installation and non-local deployment require HTTPS.
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

On Linux x86_64, the first `make` downloads the official Audiveris 5.10.2 package (about 68 MiB), verifies its checksum, and extracts its bundled Java runtime under `.tools/`. Later runs reuse that local copy. An existing `audiveris` command on `PATH` takes precedence. Set `Omr__AudiverisExecutable` to use another launcher and skip the automatic install. `Omr__TimeoutSeconds` controls the recognition timeout and defaults to 180 seconds:

```bash
Omr__AudiverisExecutable=/opt/audiveris/bin/Audiveris make
```

Image recognition is approximate. Check imported pitches, rhythm, and fingerings against the source image before relying on playback or practice grading.

To open the app from a laptop on the same local network, run this command on the server:

```bash
dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj --urls http://0.0.0.0:5080
```

On the laptop, open `http://<server-LAN-IP>:5080` (for example, `http://192.168.0.74:5080` for `archie`). Allow incoming TCP port 5080 through the server firewall if the page does not load. Keyboard input, rendering, and audio run on the laptop. The app and audio work over LAN HTTP; PWA installation and offline caching require HTTPS. See [docs/remote-access.md](docs/remote-access.md) for the SSH tunnel option, which does not expose port 5080 to the LAN.

The browser keyboard listener belongs to the focused play surface. It ignores form fields and does not register Space, Tab, Enter, Escape, Page Up/Down, or the arrow keys.

## Controls

| Function | Desktop | Browser |
|---|---|---|
| Notes | `A W S E D F R J U K I L ;` | `A W S E D F R J U K I L ;` |
| Clear notes | Space | `C` |
| Octave down/up | Arrow Down/Up | `Z` / `X` |
| Select octave | `1` through `8` | `1` through `8` |
| Toggle staff/roll | Tab | `V` |
| Play/restart score | `P` | `P` |
| Previous/next measure group | Page Up/Down | `[` / `]` |
| Start/retry practice | Enter | `T` |
| Abort practice | Escape or Space | `C` |
| Random measure | `M` | `M` |
| Exit | `Q` | Use the browser tab/window control |

Changing octave while holding a note still releases the pitch that originally started. Browser blur or tab hiding releases held notes. An active browser practice session aborts on visibility loss and can be retried with T or the visible button.

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
dotnet build PianoMapper.slnx --configuration Release
```

The manual browser checklist and current evidence are in [docs/browser-test-matrix.md](docs/browser-test-matrix.md).

## Current limits

- Multipart scores, tuplets, grace notes, tempo/time-signature changes, stem values `none`/`double`, and unsupported MusicXML semantics fail with a readable error. Repeat barlines are accepted, but playback remains linear.
- OMR output depends on scan quality and Audiveris recognition. The image importer corrects the narrow beginner-score case where an isolated fingering `3` is exported as an unbeamed quarter-note triplet; other recognition mistakes require correction in an external score editor.
- Web MIDI, a touch piano, accounts, backend synchronization, and mobile-specific layout are outside the current browser release.
- The desktop app uses OpenAL PCM synthesis. The browser uses Web Audio synthesis with equivalent note lifecycle, not matching PCM output byte for byte.
- Desktop note-off stops its source immediately and can produce a small click. The browser applies a short release envelope.

For remote desktop and browser use, see [docs/remote-access.md](docs/remote-access.md).
