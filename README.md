# PianoMapper

A small C# console app that maps computer keyboard keys to piano notes and synthesizes audio with OpenAL. 
It generates PCM waveforms, plays notes asynchronously, and can visualize a short waveform segment in the console.

## Features
- Keyboard-to-note mapping by octave (keys: A W S E D F R J U K I L ;)
- Octave controls (Up/Down arrows or number keys 1–8)
- Simple sine-wave synthesis with timing helpers for rhythmic durations
- Console waveform plotting via `ConsolePlot`

## Requirements
- .NET SDK 9.0
- An OpenAL-capable audio device (provided by OpenTK/OpenAL)

## Run
```bash
dotnet run --project PianoMapper/PianoMapper.csproj
```

## Notes
- Press `Spacebar` to clear active notes.
- Press `Q` to exit.
