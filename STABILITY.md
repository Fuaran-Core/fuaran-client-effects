# Stability

The version record of `Fuaran.ClientEffects`. Every package of this repository ships at the one `<Version>` in
`Directory.Build.props`. Newest first.

## How a change moves the version

The wire form is the program specification's `client-effect` document, pinned by its corpus. The public surface is
`ClientEffect`, `NavigateTarget`, `RefusalClass`, `ClientEffectRefusal` and the `ClientEffect` and `RefusalClass`
modules.

- **A new arm** widens a closed vocabulary: every exhaustive match over `ClientEffect` breaks, and a decoder that
  refuses unknown arms refuses the new one. It is breaking, arrives with a new format version of the specification's
  document and its corpus, and moves the minor version while this repository is on 0.x.
- **A change to an existing arm's bytes** is a wire change: breaking, at a new format version.
- **A new refusal detail, or a new function beside the existing ones,** is additive.

A draft slot (untagged, unpublished) carries an additive change without moving its number; a change of a higher class
than the slot already holds advances it.

## 0.1.0 — released 2026-11-21 as `v0.1.0`

The first version, cut by fuaran-core#718. It holds:

- `ClientEffect`, the eight arms: `WriteToClipboard`, `Navigate`, `PushState`, `Focus`, `Download`, `ReadFileBody`,
  `Print`, `Confirm`. The shape, the arm names and the field names are those the UI language's server-driven package
  declared, so its users migrate with an `open` (DECISIONS D2).
- `NavigateTarget` (`Self`, `Blank`), the target the `Navigate` arm carries (D3).
- `ClientEffect.kind`, the discriminator; `ClientEffect.encode` and `encodeList`, the encoder, writing the bytes the
  server-driven package wrote; `ClientEffect.decode`, the decoder over Core's `JVal`, with `ClientEffectRefusal` and
  `RefusalClass` (`missing-member`, `undeclared-member`, `unknown-effect-arm`) and `ClientEffect.readEncodings` (D1).
- The wire form is the specification's `client-effect` document at its current format version, unchanged: every
  round-trip vector of its corpus re-encodes byte-identically and every reject vector is refused for its class.

Dependencies: FSharp.Core 10.1.300, Fuaran.Core.Wire 0.44.1.

Released 2026-11-21 as `v0.1.0`, published to nuget.org through Trusted Publishing by `publish-packages.yml`.
