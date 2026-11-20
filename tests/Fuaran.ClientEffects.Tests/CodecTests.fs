module Fuaran.ClientEffects.Tests.CodecTests

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Expecto
open Fuaran.Core
open Fuaran.ClientEffects

/// One vector of the program specification's `client-effect` family: its id, the refusal class a reject vector names
/// (`None` for a round-trip vector), the SHA-256 the specification's manifest records for its file, and the file's bytes.
type Vector = {
    Id: string
    Reject: string option
    Sha256: string
    Bytes: string
}

/// The `client-effect` family of the program specification's corpus (Fuaran-Core/fuaran-program-specification,
/// `wire-fixtures/`), copied from its manifest at specification commit 1bcaffd. A vector's file is its whole document
/// (UTF-8, no byte-order mark, no trailing newline) and every one is ASCII, so its bytes are these strings exactly. The
/// first test holds each copy to the digest the manifest records, so an edited copy cannot pass for the corpus.
let corpus: Vector list = [
    {
        Id = "client-effect/navigate"
        Reject = None
        Sha256 = "3a2517981e2ac59e3c036675b3f132a01ae9438c5058e46d1abb99e4e44447ff"
        Bytes = """{"kind":"Navigate","route":"/orders"}"""
    }
    {
        Id = "client-effect/navigate-target"
        Reject = None
        Sha256 = "e49a34d248a2fd42e24b4ac690fe8ea78e3bed22d2633592b939638651a25b6f"
        Bytes = """{"kind":"Navigate","route":"/docs/orders","target":"Blank"}"""
    }
    {
        Id = "client-effect/push-state"
        Reject = None
        Sha256 = "45af23bf18db73a9bf822b710096e3fcce4be60b7835db4b47785ccb3ff9be33"
        Bytes = """{"kind":"PushState","route":"/orders?page=2"}"""
    }
    {
        Id = "client-effect/write-to-clipboard"
        Reject = None
        Sha256 = "70ea09cb7ada23ef1f4ced5d6e006a3402fbfd68f2bfeb5d73fa68c5ad32b5e7"
        Bytes = """{"kind":"WriteToClipboard","text":"ORD-4417"}"""
    }
    {
        Id = "client-effect/focus"
        Reject = None
        Sha256 = "6a516f9e2a2c51718a10b989a0d4dfa89bea31c28a3761d8a3f873ff1741a447"
        Bytes = """{"kind":"Focus","nodeId":"orders-search"}"""
    }
    {
        Id = "client-effect/download"
        Reject = None
        Sha256 = "c930eae6c6c06b2ad8aa88f87519daf2aba2465a1c99a20f03f894860be9564d"
        Bytes = """{"kind":"Download","url":"https://example.invalid/report.csv","name":"report.csv"}"""
    }
    {
        Id = "client-effect/read-file-body"
        Reject = None
        Sha256 = "58552873e201051f71ba069016cde7ca71e6ec54e04a3d874426ad3172a3844b"
        Bytes = """{"kind":"ReadFileBody","nodeId":"upload-1","encoding":"Text"}"""
    }
    {
        Id = "client-effect/reject-unknown-kind"
        Reject = Some "unknown-effect-arm"
        Sha256 = "0cbc0d7ed1ec4072b6f2631db972b2a18f9ebf5db184b2baf9c203cb2d585a41"
        Bytes = """{"kind":"OpenWindow","url":"https://example.invalid/"}"""
    }
    {
        Id = "client-effect/reject-navigate-target"
        Reject = Some "undeclared-member"
        Sha256 = "ce024fc5ab34797aa59c2fbb0a444e58421366dc04b451bb9d503a300791c411"
        Bytes = """{"kind":"Navigate","route":"/docs/orders","target":"Popup"}"""
    }
    {
        Id = "client-effect/control-characters"
        Reject = None
        Sha256 = "a16760fcecadde59698086b42cebe6a1f6cd6773a4a7c930d4f966b8bb3fc83a"
        Bytes = """{"kind":"WriteToClipboard","text":"ORD-4417\tGBP 1250\r\nORD-4418\tGBP 900"}"""
    }
    {
        Id = "client-effect/print"
        Reject = None
        Sha256 = "b29fc125e8792a31894e6a9af5e5e20a0da6e90f066c1308402a9cbf130a16ac"
        Bytes = """{"kind":"Print"}"""
    }
    {
        Id = "client-effect/confirm"
        Reject = None
        Sha256 = "064d8d89600a01d6d883c451729e7a800e4ee8374eb6caecd5cbbf414bbff49d"
        Bytes = """{"kind":"Confirm","prompt":"Settle ORD-4417 for GBP 1250?","token":"btn-settle#0"}"""
    }
    {
        Id = "client-effect/reject-print-member"
        Reject = Some "undeclared-member"
        Sha256 = "4d06e408fae2e1cbd26461059a5df1b4de7dc297947682df456659a8093560e8"
        Bytes = """{"kind":"Print","copies":2}"""
    }
    {
        Id = "client-effect/reject-confirm-missing-token"
        Reject = Some "missing-member"
        Sha256 = "47d8d0871918ad1ace886dc1740f4ad48ffb76dfce7cbfc675c38b8ded1679c8"
        Bytes = """{"kind":"Confirm","prompt":"Settle ORD-4417 for GBP 1250?"}"""
    }
    {
        Id = "client-effect/reject-navigate-empty-route"
        Reject = Some "missing-member"
        Sha256 = "d179d377c4b9aa5dc024a0c2d51c07704471cdfbe4191a0d99ba89759640e79f"
        Bytes = """{"kind":"Navigate","route":""}"""
    }
    {
        Id = "client-effect/reject-push-state-empty-route"
        Reject = Some "missing-member"
        Sha256 = "219f5dd1e27fda9d821d08074d93d04734ec69f10e9d164a0bc4939644ae218c"
        Bytes = """{"kind":"PushState","route":""}"""
    }
    {
        Id = "client-effect/reject-focus-empty-node-id"
        Reject = Some "missing-member"
        Sha256 = "0853a84a7c93bc6f6bba520ac3e748768126eb54a8a207904457cb73b357efe5"
        Bytes = """{"kind":"Focus","nodeId":""}"""
    }
    {
        Id = "client-effect/reject-read-file-body-empty-node-id"
        Reject = Some "missing-member"
        Sha256 = "65c7eec6f3bc3f041be365fba48e3908f0c08b9b59bb5522e93e5236ad9cbf75"
        Bytes = """{"kind":"ReadFileBody","nodeId":"","encoding":"Text"}"""
    }
    {
        Id = "client-effect/reject-download-empty-url"
        Reject = Some "missing-member"
        Sha256 = "5f782f4ba2b703603af531cbbbaad860c40ec8d2f2d9a6bc9533fa34109a908e"
        Bytes = """{"kind":"Download","url":"","name":"report.csv"}"""
    }
    {
        Id = "client-effect/reject-download-empty-name"
        Reject = Some "missing-member"
        Sha256 = "f06f7f25e63ac65f388763ed63f239ff369c50b734a5299453afb5e477c6dfea"
        Bytes = """{"kind":"Download","url":"https://example.invalid/report.csv","name":""}"""
    }
    {
        Id = "client-effect/reject-confirm-empty-prompt"
        Reject = Some "missing-member"
        Sha256 = "8e79c0de49c72455c4a5f97a1eea31d6dd2d760426052b19e568bb37a082cd60"
        Bytes = """{"kind":"Confirm","prompt":"","token":"btn-settle#0"}"""
    }
    {
        Id = "client-effect/reject-confirm-empty-token"
        Reject = Some "missing-member"
        Sha256 = "43334844a55d270ab6bf95cbc5b43140a5875f4d859d0904f2bddb6877356eb1"
        Bytes = """{"kind":"Confirm","prompt":"Settle ORD-4417 for GBP 1250?","token":""}"""
    }
]

let private sha256Hex (text: string) : string =
    SHA256.HashData(Encoding.UTF8.GetBytes text)
    |> Array.map (fun b -> b.ToString "x2")
    |> String.concat ""

let private parse (bytes: string) : JVal =
    match Json.parse bytes with
    | Ok value -> value
    | Error e -> failwithf "the document does not parse: %s" e

/// Decode and re-encode: `Ok` carries the bytes this codec writes back, `Error` the refusal class's wire spelling.
let private roundTrip (bytes: string) : Result<string, string> =
    parse bytes
    |> ClientEffect.decode
    |> Result.map ClientEffect.encode
    |> Result.mapError (fun r -> RefusalClass.id r.Class)

let private checkVector (v: Vector) =
    match v.Reject, roundTrip v.Bytes with
    | None, Ok bytes -> Expect.equal bytes v.Bytes $"{v.Id} re-encodes byte-identically"
    | None, Error cls -> failtestf "%s is a round-trip vector and was refused as %s" v.Id cls
    | Some expected, Error cls -> Expect.equal cls expected $"{v.Id} is refused for its class"
    | Some expected, Ok bytes -> failtestf "%s must be refused as %s and decoded to %s" v.Id expected bytes

let private text (e: JsonElement) (name: string) : string =
    e.GetProperty(name).GetString() |> nonNull

/// The live check: the embedded copy is the manifest's `client-effect` family exactly, and every file the manifest
/// lists passes against the codec. Registered only when FUARAN_PROGRAM_SPEC_CORPUS names a `wire-fixtures` directory.
let private liveCorpus (dir: string) =
    use manifest =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")))

    let listed = [
        for v in manifest.RootElement.GetProperty("vectors").EnumerateArray() do
            let atToySubject =
                match v.TryGetProperty "subject" with
                | true, _ -> true
                | _ -> false

            if text v "document" = "client-effect" && not atToySubject then
                {
                    Id = text v "id"
                    Reject =
                        if text v "kind" = "reject" then
                            Some(text v "reject")
                        else
                            None
                    Sha256 = text v "sha256"
                    Bytes = File.ReadAllText(Path.Combine(dir, text v "file"), UTF8Encoding false)
                }
    ]

    Expect.isNonEmpty listed "the manifest lists a client-effect family"
    let key (v: Vector) = v.Id, v.Reject, v.Sha256, v.Bytes

    Expect.equal
        (listed |> List.map key |> Set.ofList)
        (corpus |> List.map key |> Set.ofList)
        "the embedded copy is the family"

    listed |> List.iter checkVector

let private samples: ClientEffect list = [
    ClientEffect.WriteToClipboard "x"
    ClientEffect.Navigate("/r", NavigateTarget.Self)
    ClientEffect.Navigate("/r", NavigateTarget.Blank)
    ClientEffect.PushState "/r"
    ClientEffect.Focus "n"
    ClientEffect.Download("/u", "f.csv")
    ClientEffect.ReadFileBody("n", "Text")
    ClientEffect.Print
    ClientEffect.Confirm("p?", "t#0")
]

let private refused (bytes: string) : string =
    match roundTrip bytes with
    | Error cls -> cls
    | Ok b -> failtestf "%s decoded to %s" bytes b

[<Tests>]
let tests =
    testList "Fuaran.ClientEffects" [
        testList "the program specification's client-effect corpus" [
            test "every embedded vector is the manifest's bytes" {
                for v in corpus do
                    Expect.equal (sha256Hex v.Bytes) v.Sha256 $"{v.Id} hashes to the manifest's digest"
            }
            test "every arm is certified by a round-trip vector, and reject vectors are present" {
                let arms =
                    corpus
                    |> List.filter (fun v -> v.Reject.IsNone)
                    |> List.choose (fun v ->
                        match parse v.Bytes |> ClientEffect.decode with
                        | Ok e -> Some(ClientEffect.kind e)
                        | Error _ -> None)
                    |> Set.ofList

                Expect.equal arms (samples |> List.map ClientEffect.kind |> Set.ofList) "all eight arms"
                Expect.isTrue (corpus |> List.exists (fun v -> v.Reject.IsSome)) "reject vectors are present"
            }
            yield! corpus |> List.map (fun v -> test v.Id { checkVector v })
            match Environment.GetEnvironmentVariable "FUARAN_PROGRAM_SPEC_CORPUS" |> Option.ofObj with
            | None
            | Some "" -> ptest "the embedded copy is the live manifest's family (set FUARAN_PROGRAM_SPEC_CORPUS)" { () }
            | Some dir -> test "the embedded copy is the live manifest's family" { liveCorpus dir }
        ]
        testList "the codec's laws" [
            test "kind is the first member the encoder writes, and decode inverts encode" {
                for e in samples do
                    match parse (ClientEffect.encode e) with
                    | JObj(("kind", JStr k) :: _) -> Expect.equal k (ClientEffect.kind e) "the first member is kind"
                    | other -> failtestf "not a kind-first object: %A" other

                    Expect.equal (parse (ClientEffect.encode e) |> ClientEffect.decode) (Ok e) "decode inverts encode"
            }
            test "Navigate omits the target at Self" {
                Expect.equal
                    (ClientEffect.encode (ClientEffect.Navigate("/a", NavigateTarget.Self)))
                    """{"kind":"Navigate","route":"/a"}"""
                    "Self is never written"
            }
            test "an explicit Self target is accepted and re-encodes without it" {
                Expect.equal
                    (roundTrip """{"kind":"Navigate","route":"/a","target":"Self"}""")
                    (Ok """{"kind":"Navigate","route":"/a"}""")
                    "Self is the identity"
            }
            test "the string writer escapes quotes, backslashes and every control character, and nothing else" {
                let content = "a\"b\\c\n\r\t\u0001\u001f\u000cé"

                Expect.equal
                    (ClientEffect.encode (ClientEffect.WriteToClipboard content))
                    "{\"kind\":\"WriteToClipboard\",\"text\":\"a\\\"b\\\\c\\n\\r\\t\\u0001\\u001f\\u000cé\"}"
                    "short escapes for three, lower-case \\u00XX for the rest"

                Expect.equal
                    (parse (ClientEffect.encode (ClientEffect.WriteToClipboard content))
                     |> ClientEffect.decode)
                    (Ok(ClientEffect.WriteToClipboard content))
                    "and it reads back"
            }
            test "clipboard text may be empty" {
                let empty = """{"kind":"WriteToClipboard","text":""}"""
                Expect.equal (roundTrip empty) (Ok empty) "content, not a name"
            }
            test "encodeList is a JSON array of the encodings" {
                Expect.equal (ClientEffect.encodeList []) "[]" "empty"

                Expect.equal
                    (ClientEffect.encodeList [ ClientEffect.Print; ClientEffect.Focus "n" ])
                    """[{"kind":"Print"},{"kind":"Focus","nodeId":"n"}]"""
                    "two"
            }
            test "documents that are not effects are refused for their class" {
                Expect.equal (refused "[]") "missing-member" "not an object"
                Expect.equal (refused """{"route":"/a"}""") "missing-member" "no kind"
                Expect.equal (refused """{"kind":7}""") "missing-member" "a kind that is not a string"
                Expect.equal (refused """{"kind":"Focus","nodeId":3}""") "missing-member" "a member of the wrong type"

                Expect.equal
                    (refused """{"kind":"Navigate","route":"/a","target":1}""")
                    "undeclared-member"
                    "a target that is not a string"

                Expect.equal
                    (refused """{"kind":"ReadFileBody","nodeId":"n","encoding":"Hex"}""")
                    "undeclared-member"
                    "an encoding outside the three"

                Expect.equal
                    (refused """{"kind":"Focus","nodeId":"n","extra":true}""")
                    "undeclared-member"
                    "a member the arm does not declare"
            }
        ]
    ]

[<EntryPoint>]
let main argv = runTestsInAssemblyWithCLIArgs [] argv
