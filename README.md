# PianoMapper

A small C# app that maps computer keyboard keys to piano notes, synthesizes audio with OpenAL, and renders played notes on a live grand staff or scrolling piano-roll in an OpenTK window.
It generates PCM waveforms, plays notes asynchronously, and keeps both notation views synced with playback.

## Features
- Keyboard-to-note mapping by octave (keys: A W S E D F R J U K I L ;); notes sustain while held, up to their natural decay
- Octave controls (Up/Down arrows or number keys 1–8)
- Piano-style multi-harmonic PCM synthesis with explicit note-value, meter, and tempo conversion
- Live grand-staff notation (the default view) with clefs, ledger lines, and accidentals
- Toggleable scrolling piano-roll showing note pitch and duration in sync with playback
- MusicXML score loading for a strict piano subset (single part, up to two staves, chords, ties, rests, and backup/forward timing)
- Measure-based score notation with score playback and a tempo-synced cursor
- Play-along practice mode with a one-measure count-in, live verdict colors, and a final accuracy summary
- Live oscilloscope tracking the actual OpenAL playback position of the most recently played note
- Ad-hoc console waveform plotting via `ConsolePlot` (`PCM.VisualizeWave`, not wired into the main loop)

## Requirements
- .NET SDK 10.0
- An OpenAL-capable audio device (provided by OpenTK/OpenAL)
- A display for the OpenTK window

## Run
```bash
dotnet run --project PianoMapper/PianoMapper.csproj

# Load an uncompressed MusicXML score
dotnet run --project PianoMapper/PianoMapper.csproj -- --score path/to/piece.musicxml
```

## Test
```bash
dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj
```

## Notes
- Releasing a mapped key stops its note and records the measured hold duration for both notation views.
- Note-off currently uses an immediate source stop, which can produce a small click; a short gain ramp is a future refinement.
- Press `Spacebar` to clear active notes.
- Press `Tab` to toggle between the grand staff and piano-roll.
- Press `M` to play a generated A4 measure using typed meter, tempo, and note values.
- With a score loaded, press `P` to play/restart it and `PgUp`/`PgDn` to scroll measures.
- Press `Enter` with a score loaded to start practice or retry from the summary. `Escape` or `Spacebar` aborts practice back to free play; `Escape` exits only when practice is inactive.
- The mapped `R` key continues to play F# during score playback and practice.
- Compressed `.mxl`, multipart scores, tuplets, grace notes, and other unsupported timing/pitch semantics fail with a readable error and the app falls back to live mode.
- Press `Q` to exit.
