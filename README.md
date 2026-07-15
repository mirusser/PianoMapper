# PianoMapper

PianoMapper maps a computer keyboard to piano notes and renders performances as notation. The repository contains an OpenTK/OpenAL desktop app and a standalone Blazor WebAssembly app. Both clients share music, score, timing, grading, and layout code from `PianoMapper.Core`.

## Features

- Thirteen chromatic note keys with sustained note-on/note-off behavior and octave selection.
- Live grand staff and scrolling piano roll with clefs, ledger lines, accidentals, and note duration.
- Strict uncompressed MusicXML import for one part and up to two staves, including chords, ties, rests, and backup/forward timing.
- Scheduled score playback, measure navigation, a tempo cursor, and random-measure playback.
- Count-in practice with pitch/timing/duration verdicts and an accuracy summary.
- Piano-style multi-harmonic synthesis, oscilloscope, and spectrum.
- Browser-local Web Audio, static publishing, and offline PWA startup after the first online load.

## Requirements

- .NET SDK 10.0.
- Desktop app: an OpenAL-capable audio device and a display.
- Browser app: a current desktop browser with WebAssembly, Web Audio, and Canvas 2D. PWA installation and non-local deployment require HTTPS.

## Run the desktop app

```bash
dotnet run --project PianoMapper/PianoMapper.csproj

# Load an uncompressed MusicXML score at startup
dotnet run --project PianoMapper/PianoMapper.csproj -- --score path/to/piece.musicxml
```

## Run the browser app

```bash
dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj
```

Open the URL printed by the development server. Select **Enable audio** before playing; browser autoplay policy requires that user action. Choose a `.musicxml` or `.xml` file with the on-page picker. The browser rejects files larger than 10 MiB before parsing.

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

## Test

```bash
dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj
dotnet build PianoMapper.slnx --configuration Release
```

The manual browser checklist and current evidence are in [docs/browser-test-matrix.md](docs/browser-test-matrix.md).

## Current limits

- Compressed `.mxl`, multipart scores, tuplets, grace notes, tempo/time-signature changes, and unsupported MusicXML semantics fail with a readable error.
- Web MIDI, a touch piano, accounts, backend synchronization, and mobile-specific layout are outside the current browser release.
- The desktop app uses OpenAL PCM synthesis. The browser uses Web Audio synthesis with equivalent note lifecycle, not matching PCM output byte for byte.
- Desktop note-off stops its source immediately and can produce a small click. The browser applies a short release envelope.

For remote desktop and browser use, see [docs/remote-access.md](docs/remote-access.md).
