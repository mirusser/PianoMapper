# PianoMapper

A small C# app that maps computer keyboard keys to piano notes, synthesizes audio with OpenAL, and renders a live scrolling piano-roll in an OpenTK window.
It generates PCM waveforms, plays notes asynchronously, and shows a piano-roll of active/recent notes synced with playback.

## Features
- Keyboard-to-note mapping by octave (keys: A W S E D F R J U K I L ;)
- Octave controls (Up/Down arrows or number keys 1–8)
- Simple sine-wave synthesis with timing helpers for rhythmic durations
- Live scrolling piano-roll (OpenTK/OpenGL) showing note pitch and duration in sync with playback
- Live oscilloscope tracking the actual OpenAL playback position of the most recently played note
- Ad-hoc console waveform plotting via `ConsolePlot` (`PCM.VisualizeWave`, not wired into the main loop)

## Requirements
- .NET SDK 10.0
- An OpenAL-capable audio device (provided by OpenTK/OpenAL)
- A display for the OpenTK window

## Run
```bash
dotnet run --project PianoMapper/PianoMapper.csproj
```

## Test
```bash
dotnet test PianoMapper.Tests/PianoMapper.Tests.csproj
```

## Notes
- Press `Spacebar` to clear active notes.
- Press `Q` to exit.
