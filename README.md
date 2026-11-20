# Fuaran.ClientEffects

The client effect vocabulary of the Fuaran stack: the effect type, its discriminator and its wire codec,
declared once so that a server emitting effects, a browser performing them and a host mirroring them agree by
construction.

Apache-2.0. See `LICENSE` and `NOTICE`.

## What it holds

A client effect is something a server decides and a client performs, because it has no server-side form: a server has
no clipboard, no address bar and no printer.

| Arm | Wire form |
|---|---|
| `WriteToClipboard text` | `{"kind":"WriteToClipboard","text":…}` |
| `Navigate(route, target)` | `{"kind":"Navigate","route":…}`, with `"target":"Blank"` only when the target is `Blank` |
| `PushState route` | `{"kind":"PushState","route":…}` |
| `Focus nodeId` | `{"kind":"Focus","nodeId":…}` |
| `Download(url, name)` | `{"kind":"Download","url":…,"name":…}` |
| `ReadFileBody(nodeId, encoding)` | `{"kind":"ReadFileBody","nodeId":…,"encoding":…}`, encoding one of `Text`, `Base64`, `DataUrl` |
| `Print` | `{"kind":"Print"}` |
| `Confirm(prompt, token)` | `{"kind":"Confirm","prompt":…,"token":…}` |

```fsharp
open Fuaran.ClientEffects

ClientEffect.encode (ClientEffect.Navigate("/orders", NavigateTarget.Blank))
// {"kind":"Navigate","route":"/orders","target":"Blank"}

// Decoding takes a value Core's JSON reader produced; parsing, and its limits, are the host's.
match Fuaran.Core.Json.parse """{"kind":"Print"}""" with
| Ok value -> ClientEffect.decode value // Ok Print
| Error e -> failwith e
```

A refused document reports its class as the specification spells it (`RefusalClass.id`): `missing-member`,
`undeclared-member` or `unknown-effect-arm`.

## The wire form

The bytes are the [Fuaran program specification](https://github.com/Fuaran-Core/fuaran-program-specification)'s
`client-effect` document, and its conformance corpus pins them. The test suite runs that corpus against this codec:
every round-trip vector decodes and re-encodes byte-identically, and every reject vector is refused for its class.

## Dependencies

FSharp.Core and `Fuaran.Core.Wire`. No runtime, no UI language. The library is Fable-clean, and its package ships its
source under `fable/` for Fable consumers.

## Building

`pwsh ./run.ps1` restores the tools, checks the formatting, builds, runs the tests and compiles the library with Fable.
It needs PowerShell 7 and the .NET SDK that `global.json` pins. The decisions are in [DECISIONS.md](DECISIONS.md), and
the version record in [STABILITY.md](STABILITY.md).
