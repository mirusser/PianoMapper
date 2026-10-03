# W3C MusicXML sample files

These 18 `.musicxml` files are the official sample files of the W3C Music Notation Community Group's
MusicXML repository, copied **byte for byte and unmodified**. They are the real-world fixtures for
`MusicXmlScoreReader`; `W3cSampleScoreReaderTests` pins what the reader does with each one. Do not
edit them: if a test needs a variation, derive it in the test or write a separate minimal snippet.

- Source repository: <https://github.com/w3c/musicxml> (GitHub now redirects it to
  <https://github.com/w3c-cg/musicxml>), branch `gh-pages`
- Upstream commit the files were fetched at: `22680df87e4b2faa70f531568a4701ce7792e0a5`
  (committed 2026-10-01T16:11:42Z, "Fix for sidemenu scroll issue and related improvements")
- Fetched from: `https://raw.githubusercontent.com/w3c/musicxml/22680df87e4b2faa70f531568a4701ce7792e0a5/<repo path>`;
  every file was downloaded twice (once from the `gh-pages` branch tip, once pinned to the commit
  above) and the copies compared byte-identical.
- Most files declare `version="4.1"`, so they follow the 4.1 draft carried by the repository; the only
  tagged release is `v4.0`.

## License

The MusicXML schema header states: "Copyright (c) 2004-2021 the Contributors to the MusicXML
Specification, published by the W3C Music Notation Community Group under the W3C Community Final
Specification Agreement (FSA)":

- <https://www.w3.org/community/about/agreements/final/> (currently redirects to
  <https://www.w3.org/community/about/process/final/>)
- Human-readable summary: <https://www.w3.org/community/about/agreements/fsa-deed/>

The repository itself carries no separate `LICENSE` file at the pinned commit; the schema header above
is the license statement these samples are published under.

## Files, original repository paths and SHA-256

`W3cSampleScoreReaderTests` recomputes each hash, so an edited copy fails the test.

| File here | Original path in the repository | SHA-256 |
|---|---|---|
| `accidental-element-multiple.musicxml` | `docs/src/data/examples/musicxml/accidental-element-multiple.musicxml` | `555e4bdb0230d722f205a57b409d15d0e490455550cb9318a4231c8d3f7e16b2` |
| `barline-multiple-coda.musicxml` | `docs/src/data/examples/musicxml/barline-multiple-coda.musicxml` | `78526320cd0e0f9211d2defacf8a771b999d8cc6d5a31c75e3965a211a7fba58` |
| `harmonic-element.musicxml` | `docs/src/data/examples/musicxml/harmonic-element.musicxml` | `cd19456616d4ff420bec3a9e16f628922854a71365f3c26bda758509a11068e3` |
| `rest-and-display-step-elements.musicxml` | `docs/src/data/examples/musicxml/rest-and-display-step-elements.musicxml` | `c29af784747c4c36351afddbd61b792077d09c02fb1191ecf5431b633392fe35` |
| `tutorial-apres-un-reve.musicxml` | `docs/src/data/examples/musicxml/tutorial-apres-un-reve.musicxml` | `45cee78c7cb48e96545d1798f7d939cb2e0054d218a32d24bdd0aee7ff6b0d0f` |
| `tutorial-chopin-prelude.musicxml` | `docs/src/data/examples/musicxml/tutorial-chopin-prelude.musicxml` | `b4a138a5db01d38fdc1fa9d7320373053348d60764da1297641ace10130c5e8a` |
| `tutorial-chord-symbols.musicxml` | `docs/src/data/examples/musicxml/tutorial-chord-symbols.musicxml` | `f27174963cb5529bb57df63d264f722969840696634c0ff659a8a80758c26c5f` |
| `tutorial-hello-world.musicxml` | `docs/src/data/examples/musicxml/tutorial-hello-world.musicxml` | `679cb43f0d09569888cea535a426ed414609e417cf2b6dccb95796c5bd24553e` |
| `tutorial-percussion.musicxml` | `docs/src/data/examples/musicxml/tutorial-percussion.musicxml` | `5d3edf7109109434050379d07d6c0df388cff4c5e9a2e3d2f65520dfe8339592` |
| `tutorial-tablature.musicxml` | `docs/src/data/examples/musicxml/tutorial-tablature.musicxml` | `846254033785b40575be1352183b19635bc61a2e8591bf49daec6e83d80bb6f5` |
| `voice-direction-element.musicxml` | `docs/src/data/examples/musicxml/voice-direction-element.musicxml` | `045824079fa2400894dc6bf6bc59e4e450576e02e0b6b0b11f46a26305354c04` |
| `accidentals.musicxml` | `tests/files/accidentals.musicxml` | `766fbf7948c725909a0337978bb8ee39bc91a2690de366068167adbe925a5572` |
| `beams-ties.musicxml` | `tests/files/beams-ties.musicxml` | `cf972b5028979e2063251e598ffa3b82118680b23052dec85b7f0765af812295` |
| `beams-ties.invalid.musicxml` | `tests/files/beams-ties.invalid.musicxml` | `1f197305dd66b47eae704dcdaca2e680c4b276cff12f5b0f0723b5e7dc7901b7` |
| `parts-groups.musicxml` | `tests/files/parts-groups.musicxml` | `3913042d7803e049531db555e292d7cad5378e0b8c0ac3a7d3dfdaa1c49ad22e` |
| `parts-groups.invalid.musicxml` | `tests/files/parts-groups.invalid.musicxml` | `f183e5eebe2a8261bf3e28814284b04232911f8b9092d4fd61e15a0e0222acff` |
| `repeats-jumps.musicxml` | `tests/files/repeats-jumps.musicxml` | `6f5a1d805fc74cb1f48fb905f63cc2081d61d310613d83b5cee6dbbce007c0db` |
| `repeats-jumps.invalid.musicxml` | `tests/files/repeats-jumps.invalid.musicxml` | `b7fb33b0307313e96630ed36661aa98d146ec584eedf873d295abe1630eb20fd` |

The three `.invalid.musicxml` files are W3C's deliberately semantics-invalid variants of the valid files; they
come from the repository's `tests/assertions.json`, which pairs each with a Schematron rule under
`tests/validations/` (see <https://github.com/w3c/musicxml/tree/gh-pages/tests>):

- `beams-ties.invalid`: `<beam fan>` values are inconsistent inside one beam group.
- `repeats-jumps.invalid`: a `dalsegno` jump to a segno that is never defined, and a barline holding a segno and a
  coda together.
- `parts-groups.invalid`: a `part-symbol` whose `top-staff`/`bottom-staff` lie outside the declared `staves`.
