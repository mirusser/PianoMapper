# Piano and Music Basics

A beginner's tour of notes, notation and rhythm, tied to how PianoMapper models them. The structure follows the domain terms in [`CONTEXT.md`](../CONTEXT.md). The music theory is general knowledge, not taken from any single source. Code references point at the current source.

## 1. The keyboard: 12 notes, repeating

Piano keys repeat a pattern of **12 keys** (7 white, 5 black). The white keys are named with the letters **A B C D E F G**, then the pattern starts over.

```
black:     C#  D#      F#  G#  A#
           Db  Eb      Gb  Ab  Bb
white:   C   D   E   F   G   A   B   C ...
```

- **Landmark:** C is always the white key just left of the group of **two** black keys. A, B, C... on the keyboard is one octave.
- **Semitone (half step):** the distance to the next key, black or white. E→F and B→C are semitones because there is no black key between them.
- **Whole tone:** two semitones.
- **Sharp (#)** raises a note by a semitone, **flat (b)** lowers it. The black key between C and D is C# and also Db. These are the same key with two spellings (*enharmonic*).
- **Octave:** the same letter 12 semitones up. The pitch doubles in frequency.
- **Numbering:** a full piano has 88 keys, from A0 to C8. **Middle C is C4**, near the centre.

In the code, a `Pitch` is **letter + alteration + octave** (`PianoMapper.Core/Music/Pitch.cs`). C#4 and Db4 are deliberately different `Pitch`es, because the spelling matters on a staff. Each one derives:

- a **MIDI number** (`Pitch.cs:27`). C4 = 60 and A4 = 69, which is what a MIDI keyboard sends over USB.
- a **frequency** (`Pitch.cs:29`): `440 × 2^((midi−69)/12)`. A4 is 440 Hz, and middle C comes out at about 261.6 Hz.

The README's computer-key layouts are chromatic. `A W S E D F R J U K I L ;` is one octave in order, with the letter keys placed to echo the black/white pattern.

## 2. Intervals, scales, keys

An **interval** is the distance between two notes. It is named by counting letters, not semitones, with the starting note counted as 1. C→E is a "third". `PitchDistance.cs` does this: it compares diatonic indices and then says things like "a third higher" or "same note, an octave lower".

A **major scale** goes up by whole and half steps in this pattern: **W W H W W W H**. Starting on C, that is all white keys: C D E F G A B C. Starting on any other note, the pattern forces some black keys, and those make up that key's **key signature**:

- G major has 1 sharp (F#). D has 2 (F#, C#). F has 1 flat (Bb).
- The score model stores this as `KeyFifths`: +2 means two sharps, −3 means three flats, 0 is C major.
- Sharps are added in the order **F C G D A E B**. Flats are added in the reverse order.

A **triad** is a three-note chord: root, third and fifth. C major is C-E-G. An **inversion** puts a different chord note on the bottom. C-E-G is root position, E-G-C is 1st inversion, and G-C-E is 2nd inversion. The app's exercises can generate these.

## 3. Reading the staff

A **staff** has 5 lines. Each line or space is the next letter name up. That is why the code places a note with `DiatonicIndex = octave*7 + letter` (`Pitch.cs:31`).

The piano uses a **grand staff**: a **treble clef** staff for the right hand (usually) and a **bass clef** staff for the left.

```
 F5 ━━━━━━━━━━━━━━━━━━  ┐
 E5                      │
 D5 ━━━━━━━━━━━━━━━━━━  │
 C5                      │  TREBLE (G clef)
 B4 ━━━━━━━━━━━━━━━━━━  │  lines: E G B D F
 A4                      │  "Every Good Boy Does Fine"
 G4 ━━━━━━━━━━━━━━━━━━  │  spaces: F A C E
 F4                      │
 E4 ━━━━━━━━━━━━━━━━━━  ┘  ← bottom line
 D4
 C4        ━━━━            ← MIDDLE C, on its own short "ledger line"
 B3
 A3 ━━━━━━━━━━━━━━━━━━  ┐  ← top line
 G3                      │
 F3 ━━━━━━━━━━━━━━━━━━  │  BASS (F clef)
 E3                      │  lines: G B D F A
 D3 ━━━━━━━━━━━━━━━━━━  │  "Good Boys Do Fine Always"
 C3                      │  spaces: A C E G
 B2 ━━━━━━━━━━━━━━━━━━  │
 A2                      │
 G2 ━━━━━━━━━━━━━━━━━━  ┘  ← bottom line
```

The repo anchors the two staves exactly this way: treble bottom line = E4, bass bottom line = G2 (`PianoMapper.Core/Rendering/GrandStaffLayout.cs:43-44`). A note's position is just its distance in letter-steps from that bottom line.

- **Ledger lines** extend the staff for notes above or below it. Middle C sits one ledger line below the treble staff and one above the bass staff. It is the same key either way, and the staff choice only changes how it is written.
- **Live-play heuristic:** when you play freely, the app draws MIDI 60 and up on treble and everything lower on bass (`GrandStaffLayout.cs:53`). Imported scores keep the composer's explicit staff.
- **Accidentals** (♯ ♭ ♮) go before a note and last until the end of that measure. The **key signature** at the start of the line applies them to every note of that letter, in every octave.
- **8va** is a bracket meaning "play an octave higher". The renderer draws it, and the app keeps both the written and the sounding pitch.

## 4. Rhythm: how long, not just which note

A **note value** is the length of a note relative to a whole note. In 4/4 time, where the beat is a quarter note:

| Note | Fraction of whole | Beats (4/4) |
|---|---|---|
| whole | 1 | 4 |
| half | 1/2 | 2 |
| quarter | 1/4 | 1 |
| eighth | 1/8 | ½ |
| sixteenth | 1/16 | ¼ |

Modifiers (these are exactly the fields of `NoteValue`):

- **A dot** adds half of the note's own length, so a dotted quarter is 1½ beats. The code uses `2 − 1/2^dots`, so a double dot gives ×1.75.
- **A tuplet** squeezes extra notes into the time of fewer. A triplet is 3 notes in the time of 2, so each triplet eighth lasts ⅓ of a beat.
- **A rest** is a silence with its own value.
- **A tie** joins two notes of the same pitch into one longer sound. A **slur** joins different pitches into a smooth phrase (legato). They look alike but mean different things.

**Time signature** = beats per measure over the beat unit.

- 4/4 means four quarter-note beats per measure.
- 3/4 means three quarter-note beats (a waltz).
- 6/8 means six eighth-note beats, but they are felt as **two groups of three**. That is *compound* time, and the metronome code accounts for it (`PianoMapper.Core/Music/MetronomeGrid.cs:13`: 6/8, 9/8 and 12/8 click every 3).

**Tempo** is beats per minute. Duration in seconds = `beats × 60 / BPM`. MusicXML tempo marks are given per quarter note, so in 6/8 they must be converted to the eighth-note beat.

**Measures** are the units between barlines. **Repeat signs** and numbered **endings** (1st and 2nd time) are also imported.

## 5. Playing it

- **Fingering:** the thumb is 1 and the pinky is 5, on both hands. The classic start is **five-finger position**: right thumb on middle C gives C D E F G = 1 2 3 4 5. The left hand mirrors it with the pinky on the lowest note. The app's "five-note ranges" exercises train this. A fingering also decides which hand plays what: the score keeps R/L assignments separate from which staff a note is drawn on.
- **Articulation** (how a note is attacked) is part of the supported notation:
  - staccato: short and detached
  - tenuto: held full value
  - accent: stressed
  - fermata: hold longer than written
  - trill mark: rapidly alternate with the neighbour note
  - arpeggio (wavy line): roll the chord up
  - glissando: slide

## 6. How the app grades you

Reading music means answering three separate questions for each note. These are the app's `GradedAxes`: **Pitch** (the right key?), **Onset** (on the beat?) and **Duration** (held for the written value?). The `Verdict` values then describe the outcome:

- `Correct`
- `WrongPitch`
- `Early` / `Late`
- `TooShort` / `TooLong`
- `Missed` / `Extra`

**Wait for me** pacing is rhythm-free note-finding. **Play along** adds the clock.

## Suggested path with a MIDI keyboard

1. Learn the C-position (C4 to G4) with each hand. In the app, use **Pitch only** with five-note ranges, treble and bass separately.
2. Learn the rest of the letters as landmarks: C4 (middle), G4 (the clef's curl), F3 (the bass clef's dots).
3. Add rhythm: quarters and halves in 4/4, then **Pitch + rhythm**, then **Pitch + hold**.
4. Add one sharp or flat at a time through key signatures (G, F, D).
5. Move on to triads and inversions, then 3/4 and 6/8.

For deeper free material, musictheory.net (lessons plus drills) and Open Music Theory are good online references.
