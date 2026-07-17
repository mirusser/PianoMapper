[rendering] Don't infer note-trail direction from timeline scrolling — keep the note head at onset and grow duration to the right. (cause: onset anchor misunderstood)
[rendering] Don't clip only noteheads and accidentals at the clef — include note-owned ledger lines in the same clip region. (cause: ledger lines rendered with staff lines)
[rendering] Don't equate a glyph's bounding-box center with its musical anchor — verify the treble-clef G loop visually before changing scene Y. (cause: glyph center mistaken for G-line anchor)
