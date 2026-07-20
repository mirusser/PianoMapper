# PianoMapper Domain Context

This glossary is the canonical language for the piano-learning domain. Code, tests, and documentation should use these terms consistently.

| Term | Meaning | Deliberately excludes |
|---|---|---|
| **Pitch** | A spelled musical pitch made from a note letter, accidental alteration, and scientific-pitch octave, such as C#4 or Db4. MIDI number, frequency, and diatonic position are derived from it. | Stored frequency or MIDI data; enharmonic equality (C#4 and Db4 remain differently spelled pitches). |
| **NoteValue** | A rhythmic value represented by a denominator (whole through sixteenth) and optional dots. | Tempo, wall-clock duration, onset, and pitch. |
| **TimeSignature** | The number of beats in a measure and the NoteValue that counts as one beat. | Tempo and the notes contained in a measure. |
| **Tempo** | Beats per minute relative to the TimeSignature's beat NoteValue. | Meter, note values, and a running clock. |
| **MetronomeGrid** | An audio-clock anchor paired with Tempo and TimeSignature that identifies beat times, nearest beats, and signed timing deviation. | Click synthesis, performed notes, and mutable playback state. |
| **Score** | The application's imported piece model: title, meter, tempo, key signature, measures, notes, and rests. Importers adapt external formats into it. | MusicXML-specific types, rendered geometry, performed notes, and audio state. |
| **ScoreNote** | One expected score event with a Pitch, NoteValue, onset, Staff, tie-to-next flag, beam state, and optional imported up/down stem direction. Chord members share an onset. | Measured player timing, audio handles, rendering coordinates, and computed fallback stem geometry. |
| **Staff** | The explicit treble or bass staff assigned to a score note. A GrandStaff displays both together. | Pitch spelling and automatic live-input staff selection. |
| **StaffPosition** | The diatonic line or space occupied by a Pitch on a Staff, including any required ledger lines. | Pixel geometry, timing, audio, and accidental rendering policy. |
| **PerformedNote** | What the player performed: Pitch, onset time, and release time, which is absent while the note is sounding. | PCM samples, OpenAL source/buffer handles, expected score data, and grading. |
| **Instrument** | The audio module that owns synthesis, OpenAL lifecycle, polyphony gain, and timeline registration behind Play, NoteOn, NoteOff, and ClearAll operations. | Keyboard routing, score scheduling, rendering, and grading decisions. |
| **PracticeSession** | One anchored play-along run through count-in, active performance, grading feedback, and completion. | Score import, audio implementation details, and free-play recording. |
| **Verdict** | A timing or practice-grading outcome: Correct, Early, and Late describe beat alignment; practice grading also uses WrongPitch, TooShort, TooLong, Missed, and Extra. | Rendering color, audio state, and mutable session state. |
| **BeatAlignment** | One performed onset classified against its nearest MetronomeGrid beat, including beat index, signed deviation, Verdict, and downbeat status. | Note duration, pitch grading, and click synthesis. |
