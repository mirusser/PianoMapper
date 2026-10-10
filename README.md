# 🎹 PianoMapper

> A local-first piano practice studio for reading, playing, importing, and improving—whether you use a MIDI piano, the on-screen keyboard, or your computer keyboard.

PianoMapper is a .NET 10 application with three clients: an OpenTK desktop app, a standalone Blazor WebAssembly PWA, and an ASP.NET Core host that adds sheet-music recognition and a shared score library. All three use the same music, score, timing, grading, and layout engine in `PianoMapper.Core`.

> **TL;DR** — Load MusicXML or a score image, play it on an 88-key piano, practice with live feedback, and build note-reading skills through adaptive exercises. Start the complete browser experience with `make`.

## ✨ What you can do

### Learn at the piano

- Generate treble, bass, or grand-staff note-reading exercises for five-note ranges, octaves, ledger lines, keys, accidentals, and triad inversions.
- Work from pitch-only recognition through rhythm and hold-duration practice. Choose **Wait for me** or a clock-driven **Play along** mode.
- See early/late feedback, review pitch, timing, and missed-note marks after a run, retry missed material, and follow an optional guided path built from your progress history.
- Use MIDI, the touch- and mouse-playable 88-key piano, or the optional computer-key layout—MIDI is never required for exercises.

### Play, hear, and follow scores

- Use browser USB MIDI with velocity-sensitive note-on/note-off input, optional Roland FP-10 output, and a configurable metronome with tap tempo, sound choices, and common irregular-meter groupings.
- Play through the desktop client with a computer-key piano, octave controls, OpenAL synthesis, oscilloscope, and spectrum.
- Follow scheduled playback, count-in practice, random measures, tempo cursor, measure navigation, and an adjustable browser score page size.
- Read a live grand staff or scrolling piano roll with clefs, ledger lines, accidentals, note duration, ties, beams, key signatures, and notation annotations.

### Bring your own music

- Import `.mxl`, `.musicxml`, and `.xml` files for piano scores, including chords, ties, rests, tuplets, beams, octave shifts, fingering, and a focused subset of articulations and ornaments.
- Import `.jpg`, `.jpeg`, and `.png` sheet music through the hosted Audiveris recognition path, then correct fingerings directly on the grand staff.
- Save, update, load, and delete imported scores in the hosted browser app’s PostgreSQL-backed library. Source files are not retained after conversion.

### Keep the browser experience local and resilient

- Install the standalone browser client as a PWA. After its first online load, the app shell can start offline.
- Keep practice history in browser storage first. On the hosted app each finished session is also saved to PostgreSQL (see [Progress storage](#-progress-storage)), so progress survives clearing the browser; the standalone PWA keeps it in browser storage alone, and the exercise generator degrades safely when storage is unavailable.
- Run MIDI, rendering, and audio in the browser connected to the piano, even when the development host runs elsewhere on your network.

## 🚀 Quick start

Choose the route that fits what you want to try.

| I want to… | Run | What it includes |
| --- | --- | --- |
| Use the complete browser app **(recommended)** | `make` | PostgreSQL, MusicXML, image recognition, and saved scores |
| Try MusicXML in a standalone browser PWA | `dotnet run --project PianoMapper.Web/PianoMapper.Web.csproj` | MusicXML import; no server, OMR, saved-score library, or server-saved progress |
| Run the desktop app | `dotnet run --project PianoMapper/PianoMapper.csproj` | OpenTK/OpenAL desktop piano app |
| Start a hosted browser app with your own prerequisites | `docker compose up --detach --wait postgres` then `dotnet run --project PianoMapper.Server/PianoMapper.Server.csproj` | Hosted browser app with PostgreSQL and image recognition |

For the complete browser app, from the repository root:

```bash
make
```

Open the URL printed by the development server, select **Enable audio**, then load a `.mxl`, `.musicxml`, `.xml`, `.jpg`, `.jpeg`, or `.png` file. The browser rejects files larger than 10 MiB before parsing or recognition.

To open a MusicXML score with the desktop app at launch:

```bash
dotnet run --project PianoMapper/PianoMapper.csproj -- --score path/to/piece.musicxml
```

### Your first session

1. Start the app with `make`, or choose one of the alternatives above.
2. Open the displayed local URL and select **Enable audio**; browsers require a user action before they allow playback.
3. Optionally select **Connect MIDI piano** and grant browser permission. For a Roland FP-10, use its square **USB COMPUTER** port with a data-capable USB cable.
4. Load a score, press **Play**, or open the note-reading exercise panel and choose a mode.

Browser MIDI requires HTTPS or localhost. The on-screen piano and optional computer-key input work without MIDI. With the **FP-10** sound source selected, the metronome click is also sent to the keyboard as short high notes (the same accents as the PC click) instead of playing through the computer speakers.

## 🧠 Practice that grows with you

The exercise panel starts with simple reading and progressively introduces rhythm, timing, and duration. Its generated material can use random, melodic, or fixed-interval patterns; five-note, octave, ledger-line, key-signature, accidental, and chord ranges; and fixed or varied rhythms.

| Practice focus | What PianoMapper checks |
| --- | --- |
| **Pitch only** | The right key, with no timing pressure |
| **Rhythm only** | Onset timing; any key can answer |
| **Pitch + rhythm** | Correct key and beat alignment |
| **Pitch + hold** | Correct key and written duration |
| **Pitch + hold + rhythm** | Pitch, onset, and duration together |

Timed exercises include adjustable tempo, a beat indicator, and an optional continuing click. In **Wait for me**, nothing counts or clicks until you play: your first correct key is beat one, so reading time never counts as lateness, and the click starts on that note. A timing gauge above the staff pulses with the click, counts the beat, and shows how far off the beat your latest notes were. If you stop for more than two beats, the next key restarts the beat (and moves the click with it) instead of counting as seconds late. Their grading anchor owns the click while they run, so controls cannot re-phase it mid-exercise. **Wait for me** pauses until the correct key is found; **Play along** counts in one measure, keeps time and records missed or extra notes. Score playback can optionally share the same click anchor. During a run, answer-revealing aids such as names, fingering, next-key highlighting, and coach hints are opt-in. Afterward, the review distinguishes pitch, timing, and missed-note issues and can generate a focused retry.

## 🎼 Scores, notation, and fingering

MusicXML import is designed for one two-staff part or two single-staff piano parts. It preserves sounding pitch for playback and grading while rendering written staff position, including supported octave-shift brackets. The renderer supports dotted values through 64th notes, tuplet timing, chords, beam groups, key signatures, ties, and a focused set of notations: fermata, staccato, tenuto, accent, staccatissimo, trill marks, accidental marks, slurs, arpeggios, and glissando/slide marks.

On the hosted path, JPEG and PNG input is converted by Audiveris into the same PianoMapper score format. Image recognition is approximate: check imported pitches, rhythm, and fingerings before relying on playback or grading. Enable **Edit fingering** under Notation to adjust individual notes, or choose **Regenerate all fingerings** for deterministic, ergonomically optimized suggestions that retain the score’s right- and left-hand assignments.

The **Fingering reach profile** (under Notation) stores independent comfortable and maximum 1–5 spans for each hand in browser-local settings, in white-key centre spacings. C4–D5 is eight spacings and is shown as an upper-reach reference, not as a measured comfort value; until you change them, both hands use the generic default of 6.5 comfortable and 9 maximum (a tenth, C4–E5). Other finger pairs keep generic limits of 5 comfortable and 7 (an octave) maximum, and never exceed your 1–5 span. Spans saved in an earlier version are kept as saved. A maximum is a hard limit for keys held together, including notes sustained from earlier, and a comfortable span only adds cost when exceeded. Released-hand leaps remain possible. If no fingering satisfies the profile, regeneration reports the measure and note and leaves the score unchanged; a key attacked again while it is still held is treated as re-struck by the finger already on it. The same profile is used for imported scores, new note-reading exercises, and **Retry missed notes**.

In fingering-edit mode, **Lock fingering** keeps a corrected number for the currently loaded score (the note list marks locked notes), and **Regenerate preserving locked fingerings** optimizes only the other notes. **Regenerate all fingerings** replaces every number and clears the locks, and loading another score drops them. Regeneration also offers up to two other coherent alternatives under **Fingering alternatives**; choosing one swaps its numbers in, and nothing is saved until you save the score. The generator does not redistribute hands, roll chords, assume pedal use, or perform silent finger substitutions.

## 🎛️ Controls

| Function | Desktop | Browser |
| --- | --- | --- |
| Play notes | `A W S E D F R J U K I L ;` | MIDI piano, on-screen piano, or optional computer-key layout: `Z S X D C V G B H N J M , L .` |
| Clear / abort practice | Space | `C` |
| Change octave | Arrow Down / Up, or `1`–`8` | On-page notation-focus controls |
| Toggle staff / piano roll | Tab | `V` |
| Play or restart score | `P` | `P` |
| Previous / next measures | Page Up / Down | `[` / `]` |
| Start / retry practice | Enter | `T` |
| Random measure | `M` | `M` |
| Exit | `Q` | Browser tab or window controls |

The browser listener belongs to the focused play surface and leaves normal form editing and browser navigation keys alone. Its computer-key toggle is off by default and takes priority over the overlapping `C`, `V`, and `M` shortcuts while enabled.

## 🛠️ Requirements and configuration

| Path | Requirements |
| --- | --- |
| All clients | .NET SDK 10.0 |
| Desktop | An OpenAL-capable audio device and a display |
| Browser | A current desktop browser with WebAssembly, Web Audio, and Canvas 2D |
| Browser USB MIDI | Web MIDI support, user permission, and HTTPS or localhost |
| `make` hosted launcher | Bash, Make, Docker, and the Docker Compose plugin |
| Automatic image recognition | Linux x86_64 plus `curl`, `ar`, `sha256sum`, `tar`, and `unzstd` |

On Linux x86_64, the first `make` downloads the official Audiveris 5.10.2 package (about 68 MiB), verifies its checksum, and extracts its bundled Java runtime under `.tools/`. Later runs reuse that copy; an existing `audiveris` command on `PATH` takes precedence. On other platforms, install Audiveris separately and point the hosted app at its launcher:

```bash
Omr__AudiverisExecutable=/opt/audiveris/bin/Audiveris make
```

The development PostgreSQL container listens only on `localhost:5434` and stores data in the `pianomapper-postgres-data` Docker volume. To use an existing PostgreSQL service instead, provide its connection string; `make` then skips the development container:

```bash
ConnectionStrings__PianoMapper='Host=db.example;Database=pianomapper;Username=pianomapper;Password=secret' make
```

`Omr__TimeoutSeconds` controls image-recognition timeout and defaults to 180 seconds.

## 📦 Publish the browser app

Publish the standalone PWA when MusicXML-only import is enough:

```bash
dotnet publish PianoMapper.Web/PianoMapper.Web.csproj --configuration Release
```

Serve `PianoMapper.Web/bin/Release/net10.0/publish/wwwroot/` as static HTTPS files. Configure the host to return `index.html` for client-side routes and preserve `<base href="/">` unless you intentionally deploy under a subdirectory.

For saved scores and image recognition, publish the ASP.NET Core companion instead and configure `ConnectionStrings__PianoMapper` before starting it:

```bash
dotnet publish PianoMapper.Server/PianoMapper.Server.csproj --configuration Release
```

The standalone PWA has no process in which to run Audiveris or connect to PostgreSQL, so it intentionally supports MusicXML import only and keeps practice history in browser storage alone. The first PWA visit must be online so its service worker can cache the app shell; selected music files are never bundled into that cache.

## 💾 Progress storage

Finished note-reading sessions are kept in two places. The browser's local storage (key `pianomapper-sight-reading-history-v1`) is read first and written first, so the Progress panel and the guided path appear instantly and work offline. On the hosted app each session is then written to the `progress_sessions` table in PostgreSQL, and on every page load the browser and the server are reconciled: sessions only the server has are added to the browser, and sessions only the browser has (old browser data, or writes made while the server was down) are uploaded. A session is never stored twice, because each carries its own id. The browser keeps the newest 100 sessions; the server keeps all of them. There is one shared history for now, with no learner id in the routes.

| Route | Behavior |
| --- | --- |
| `GET /api/progress/sessions?limit=100` | The newest sessions, newest first, as a JSON array. `limit` is 1–100 (default 100); anything else is `400`. |
| `POST /api/progress/sessions` | Stores a JSON array of sessions and answers `204`. Sending a session again changes nothing. One entry the server cannot read (malformed, or a schema version it does not know) rejects the whole request with `400`. |
| `DELETE /api/progress/sessions` | Deletes every stored session and answers `204`. The **Clear history** button calls it. |

Every database route, saved scores included, answers `503` problem details while PostgreSQL is unreachable and recovers once it is back, without a restart; invalid input (such as a blank score title) is `400`. The host starts even when PostgreSQL is down, and failures are logged without the connection string. A missing `ConnectionStrings__PianoMapper` still stops startup.

The Progress panel reports the state. With the server unreachable it says progress is **not synced** and keeps saving to the browser, and the next sync uploads what was missed. With no server at all (the standalone PWA) it behaves as before and shows no error. If the browser blocks local storage, history is held in memory and on the server only.

## 🧭 Project map

| Project | Purpose |
| --- | --- |
| `PianoMapper.Core` | Shared music model, MusicXML import, score layout, timing, playback scheduling, synthesis contracts, and exercise grading |
| `PianoMapper` | OpenTK/OpenAL desktop application |
| `PianoMapper.Web` | Standalone Blazor WebAssembly PWA, Web MIDI, Web Audio, and canvas UI |
| `PianoMapper.Server` | ASP.NET Core host for the web client, Audiveris image conversion, and PostgreSQL saved scores and progress |
| `PianoMapper.Tests` | .NET unit and integration tests, plus browser-facing JavaScript tests |

## ✅ Verify a checkout

```bash
dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj
node --test PianoMapper.Tests/JavaScript/*.test.mjs
dotnet build PianoMapper.slnx --configuration Release
```

The database tests (`Category=Integration`) start a throwaway `postgres:17-alpine` container through Testcontainers, so a bare `dotnet test` needs a running Docker daemon and fails, rather than skips, without one. Without Docker, run `dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj --filter "Category!=Integration"`; to run only the database tests, use `--filter "Category=Integration"`. They never touch the development database.

The browser’s manual coverage and current evidence live in [the browser test matrix](docs/browser-test-matrix.md). For running a browser client from another machine while keeping MIDI and audio local to the browser, see [remote access notes](docs/remote-access.md).

## ⚠️ Boundaries and current limits

- MusicXML import is intentionally focused: more than two aggregate staves, grace notes, tempo or time-signature changes, stem values `none`/`double`, and unsupported musical semantics fail with a readable error. Accepted presentation-only constructs that have no representation are reported as warnings.
- Tuplets play at the correct duration for any imported ratio, but only a beamed 3:2 triplet currently receives a rendered numeral. Imported-score rests affect spacing and timing but are not yet drawn; imported tied notes remain separate noteheads without a drawn tie curve.
- OMR output depends on scan quality and Audiveris. Review every recognized score, especially rhythm and fingering, before using it for practice.
- Timed exercises use the audio clock and do not yet compensate for output latency. Hardware with a large latency can make an on-beat player appear slightly late.
- Web MIDI depends on browser support, permission, and secure context. Sustain-pedal control changes are not interpreted; duration checking ends when a key sends note-off.
- Accounts and mobile-specific layout are outside the current browser release. The hosted server keeps one shared progress history, not one per learner. Clearing history deletes the saved copy too, but a browser that still holds older sessions re-uploads them on its next sync, so clear on each device that has used the app.

---

_Built for the satisfying loop of read → play → listen → improve._
