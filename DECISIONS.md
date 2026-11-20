# Decisions

The design decisions of `Fuaran.ClientEffects`, newest last. Each records what was decided, what was considered, and
why. A later decision that changes one says so and names it; an entry is never rewritten.

## D1 — The library holds the vocabulary, its discriminator and both halves of its codec (2026-11-20, fuaran-core#718)

**Decided.** The client effect type `ClientEffect`, its discriminator (`ClientEffect.kind`), its encoder
(`ClientEffect.encode`, `encodeList`) and its decoder (`ClientEffect.decode`, with `ClientEffectRefusal` and its three
`RefusalClass` cases) are declared here, once. They were declared in the UI language's server-driven package (the type,
the discriminator and the encoder) and in the UI tier's adapter for the program client placement (the decoder). Those
declarations are deleted when the UI language takes this package, which waits for this package's first release.

**The decoder is part of the codec.** The encoder alone cannot be held to the program specification's corpus: half of
its vectors are reject vectors, and a round-trip vector is certified by decoding it and re-encoding the result. The
rules the reject vectors pin (a closed set of eight arms, every member declared, non-empty names, a closed target set,
a closed encoding set) are the vocabulary's rules, so they belong with it. The refusal classes are spelled as the
specification spells them (`RefusalClass.id`), and a host that reports its own refusal type maps the three classes onto
it.

**Dependencies.** FSharp.Core and `Fuaran.Core.Wire`, and nothing else. The decoder reads Core's `JVal`, the value
Core's reader produces, which is the JSON value the program specification's hosts already decode from; a decoder over
text would need a JSON reader of its own. Parsing the text, and the limits a host applies while it does, stay the
host's: `decode` takes a parsed value. The encoder writes its own bytes with its own string writer, as before, because
those bytes are the corpus's and are not Core's canonical rendering.

**Fable-clean.** The source ships under `fable/` in the package, as the server-driven package shipped it, and the gate
compiles it with Fable. It is written to compile with nullness checking on, so it compiles under a consumer's
properties either way.

## D2 — The type moves outright; the server-driven package keeps no alias (2026-11-20, fuaran-core#718)

**Decided.** The type, its arms and its module live in the namespace `Fuaran.ClientEffects`. `Fuaran.UI.ServerDriven`
does not keep an alias for a minor version: when it takes this package, its declaration is deleted and every user
opens `Fuaran.ClientEffects`.

**Considered: an alias for one minor.** A type abbreviation `type ClientEffect = Fuaran.ClientEffects.ClientEffect`
in the old namespace. Rejected for three reasons. It would carry only part of the old surface: an abbreviation does not
bring the arms into scope for an unqualified pattern or constructor, and the `ClientEffect` module (the discriminator
and the encoder) would need a second, forwarding module, so a user would still edit code, only less of it. The UI
language's packages are on a 0.x line, and the change that takes this package is breaking already. And an alias is a
removal some later change has to remember, which is debt by construction. A user's migration is one `open` per file,
and at each site that builds a `Navigate` effect from a tree action, the target mapping of D3.

## D3 — The library declares the navigation target, and the UI language keeps its own (2026-11-20, fuaran-core#718)

**Decided.** `NavigateTarget` (`Self`, `Blank`) is declared here, beside the effect that carries it. The UI language's
tree vocabulary keeps its own generated `NavigateTarget`, the target of its `Navigate` action, and its lowering from
that action to this effect maps one onto the other with a total match. The tree language does not take this package.

**Considered: the target moves, and the tree language takes this package.** One declaration of a two-case enum is the
appeal. It does not hold, for a reason of where each declaration comes from. The tree language's `NavigateTarget` is
generated, with its codec, from that language's interface description, which is the one source of truth for the tree
document on every host language it generates for, and that description has no way to name a type declared in a .NET
package. Moving it would need either an import mechanism in the interface description (a change to a generic library
for one enum) or a hand-written exception to the generated layer. And it would make the tree language, which no longer
needs any effect vocabulary, depend on one.

**Why two declarations are right here.** They are the members of two documents with two owners: the tree document's
`Navigate` action and the client-effect document's `Navigate` effect. Each has its own specification and its own
corpus, and the two agree on the wire (`Self` omitted, `Blank` written) because each corpus pins it, not because the
types are shared. The mapping is a total match in the one place that lowers an action to an effect, so a third target
added to the tree language is a compile error at the mapping until this vocabulary decides whether it has one too.

## D4 — `Focus` and `ReadFileBody` keep their node address (2026-11-20, fuaran-core#718)

**Decided.** Both arms keep `nodeId: string`, and the wire is unchanged.

**The concern.** An id ties an effect to a tree: a performer must resolve it against something.

**Why it stays.** The id is an opaque address that the performing client resolves; this vocabulary does not say what it
addresses. A server-driven client resolves it against the tree it was sent, and a client-side runtime resolves it
against the elements it rendered, or against the keys its view gave them. Every client that can focus something or read
a selected file has some address for it, so the field is not a tree in disguise. Replacing it with a structured address
(an element reference, a selector) would either reach into the performer's world, which is not this vocabulary's, or
admit a selector language as untrusted wire input. The non-empty rule stands: an empty address names nothing.

## D5 — `Confirm` keeps its token (2026-11-20, fuaran-core#718)

**Decided.** `Confirm` keeps `token: string`, required and non-empty, and the wire is unchanged.

**The concern.** A client-side runtime routes a confirmation's answer back to whoever asked through its own replies, so
off the wire the token carries nothing that runtime needs.

**Why it stays.** On the wire it is the only thing that says which confirmation an answer belongs to, when one gesture
raises two of them; a server-driven exchange has no other channel. Dropping it from the wire would be a breaking change
to the specification's document, and making it optional would make the wire document's meaning depend on which kind of
client reads it. An in-process performer reads the token as an opaque label and may ignore it, and a runtime that has
no use for it supplies any non-empty label it likes. One shape serves both, and the wire stays at its format version.

## D6 — The gate holds the codec to the specification's corpus by an embedded, digest-pinned copy (2026-11-20, fuaran-core#718)

**Decided.** The tests embed the specification's `client-effect` family (every vector's id, refusal class and bytes)
and hold each copy to the SHA-256 the specification's manifest records, so an edited copy fails. Setting
`FUARAN_PROGRAM_SPEC_CORPUS` to a checkout's `wire-fixtures` directory adds a check that the copy is exactly the live
manifest's family; unset, that check is reported as skipped, never as passed.

**Considered: reading the corpus from a checkout in every run.** It would make the gate depend on a second repository's
checkout being present beside this one, at an unstated version. The digests make the copy as good as the files; the live
check is the drift probe, run when the specification moves.
