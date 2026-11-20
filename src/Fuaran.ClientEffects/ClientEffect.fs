namespace Fuaran.ClientEffects

open Fuaran.Core

// ============================================================================
//  The client effect vocabulary: the effects a server decides and a client
//  performs, because they have no server-side form (a server has no clipboard,
//  no address bar and no printer). Declared once, here, with its discriminator
//  and its wire codec, so that a server emitting effects, a client performing
//  them and a host mirroring them agree by construction.
//
//  The wire form is the program specification's `client-effect` document:
//  tagged-object camelCase JSON whose discriminator is a `kind` member (the
//  specification's enumerated envelope exception), members in declaration
//  order, and the closed set of eight arms below. Its corpus pins the bytes;
//  the tests run it against this codec.
//
//  FSharp.Core and Core's wire package only, and Fable-clean.
// ============================================================================

/// Which browsing context a navigation lands in.
///
/// A closed enum, deliberately: the axis has two answers, this context or a
/// fresh one. Frame-busting targets and named frames are not expressible.
/// `Self` is the identity and is never written on the wire.
[<RequireQualifiedAccess>]
type NavigateTarget =
    /// Navigate in the current browsing context; the default.
    | Self
    /// Open a fresh browsing context, always without an opener or referrer.
    | Blank

/// A client-only effect a client performs on the server's behalf.
type ClientEffect =
    /// Write `text` to the clipboard. The text is content, and may be empty.
    | WriteToClipboard of text: string
    /// Navigate the browser to `route`: a full page load, a real navigation.
    ///
    /// `target` names the browsing context. It is OMITTED from the encoding at
    /// `Self`, so a `{"kind":"Navigate","route":…}` written before the member
    /// existed keeps its bytes and its meaning.
    ///
    /// A field rather than a second arm: `Blank` modifies the same act, go
    /// here, rather than naming a different one. A client opens a `Blank`
    /// target with `noopener,noreferrer`.
    | Navigate of route: string * target: NavigateTarget
    /// Update the address bar to `route` without a reload (`history.pushState`).
    /// A client listening for `popstate` reports the popped route back to the
    /// server, which restores what that route shows.
    | PushState of route: string
    /// Move focus to the addressed node. `nodeId` is an opaque address the
    /// performing client resolves; this vocabulary does not interpret it.
    | Focus of nodeId: string
    /// Trigger a file download of `url`, suggested filename `name`.
    | Download of url: string * name: string
    /// Read the body of a selected file at the addressed node with the given
    /// encoding, one of `"Text"`, `"Base64"` and `"DataUrl"`; the body is
    /// reported back to whoever decided the effect.
    | ReadFileBody of nodeId: string * encoding: string
    /// Open the reader's own print dialogue. Payload-free, and the emptiness is
    /// the point: the paged medium belongs to the host and every parameter of
    /// the printing to the reader, so there is nothing for a server to name.
    /// `{"kind":"Print"}` is the whole instruction, and nothing is reported back.
    | Print
    /// Ask the reader `prompt` and report the answer. `token` names which
    /// confirmation in the originating gesture is being answered, so a gesture
    /// raising two of them is unambiguous.
    ///
    /// What a yes will DO is deliberately not in the instruction: the client
    /// is told what to ask and answers yes or no, and the party holding the
    /// state and the policy decides what that answer means.
    | Confirm of prompt: string * token: string

/// The class a decoder refusal falls in, spelled on the wire by `RefusalClass.id`.
[<RequireQualifiedAccess>]
type RefusalClass =
    /// A required member is absent, present and empty where it must name
    /// something, or not of its declared type; or the document is not an object.
    | MissingMember
    /// A member the arm does not declare, or a declared member carrying a value
    /// outside its closed set.
    | UndeclaredMember
    /// A `kind` that is not one of the eight arms.
    | UnknownEffectArm

/// Why a document is not a client effect. `Detail` names members and arms, and
/// never echoes a member's value beyond a closed-set token.
type ClientEffectRefusal = { Class: RefusalClass; Detail: string }

module RefusalClass =

    /// The program specification's spelling of the class.
    let id (cls: RefusalClass) : string =
        match cls with
        | RefusalClass.MissingMember -> "missing-member"
        | RefusalClass.UndeclaredMember -> "undeclared-member"
        | RefusalClass.UnknownEffectArm -> "unknown-effect-arm"

module ClientEffect =

    // ─── the JSON string writer ─────────────────────────────────────────────

    let private hex = "0123456789abcdef"

    let private needsEscape (c: char) = c = '"' || c = '\\' || c < ' '

    /// `s` as a JSON string literal, quotes included.
    ///
    /// The three common control characters keep their short escapes; every
    /// other character below U+0020 is written as `\u00XX` in lower-case hex,
    /// and nothing else is escaped. These are this wire's own bytes, which the
    /// corpus pins; it is not a canonical JSON writer. A string with nothing to
    /// escape is returned between quotes as it is, and clean runs are copied
    /// whole.
    ///
    /// The escaped path assembles its pieces with `String.concat` rather than
    /// a `StringBuilder`: under Fable with nullness on, `StringBuilder.ToString`
    /// lowers to an import the bundled JavaScript library does not export.
    let private quote (s: string) : string =
        let mutable first = -1
        let mutable i = 0

        while first < 0 && i < s.Length do
            if needsEscape s.[i] then
                first <- i

            i <- i + 1

        if first < 0 then
            "\"" + s + "\""
        else
            let parts = ResizeArray<string>()
            parts.Add "\""
            parts.Add(s.Substring(0, first))
            let mutable runStart = first

            for j in first .. s.Length - 1 do
                let c = s.[j]

                if needsEscape c then
                    if j > runStart then
                        parts.Add(s.Substring(runStart, j - runStart))

                    parts.Add(
                        match c with
                        | '"' -> "\\\""
                        | '\\' -> "\\\\"
                        | '\n' -> "\\n"
                        | '\r' -> "\\r"
                        | '\t' -> "\\t"
                        | _ -> "\\u00" + hex.Substring(int c >>> 4, 1) + hex.Substring(int c &&& 0xF, 1)
                    )

                    runStart <- j + 1

            if runStart < s.Length then
                parts.Add(s.Substring runStart)

            parts.Add "\""
            String.concat "" parts

    // ─── the discriminator and the encoder ──────────────────────────────────

    /// Stable discriminator string, PascalCase-matched to the arms. It is the
    /// `kind` member the encoder writes.
    let kind (effect: ClientEffect) : string =
        match effect with
        | WriteToClipboard _ -> "WriteToClipboard"
        | Navigate _ -> "Navigate"
        | PushState _ -> "PushState"
        | Focus _ -> "Focus"
        | Download _ -> "Download"
        | ReadFileBody _ -> "ReadFileBody"
        | Print -> "Print"
        | Confirm _ -> "Confirm"

    /// Encode one effect as tagged-object camelCase JSON, members in declaration order.
    let encode (effect: ClientEffect) : string =
        match effect with
        | WriteToClipboard text -> $"""{{"kind":"WriteToClipboard","text":{quote text}}}"""
        | Navigate(route, target) ->
            match target with
            | NavigateTarget.Self -> $"""{{"kind":"Navigate","route":{quote route}}}"""
            | NavigateTarget.Blank -> $"""{{"kind":"Navigate","route":{quote route},"target":"Blank"}}"""
        | PushState route -> $"""{{"kind":"PushState","route":{quote route}}}"""
        | Focus nodeId -> $"""{{"kind":"Focus","nodeId":{quote nodeId}}}"""
        | Download(url, name) -> $"""{{"kind":"Download","url":{quote url},"name":{quote name}}}"""
        | ReadFileBody(nodeId, encoding) ->
            $"""{{"kind":"ReadFileBody","nodeId":{quote nodeId},"encoding":{quote encoding}}}"""
        | Print -> """{"kind":"Print"}"""
        | Confirm(prompt, token) -> $"""{{"kind":"Confirm","prompt":{quote prompt},"token":{quote token}}}"""

    /// Encode an effect list as a JSON array.
    let encodeList (effects: ClientEffect list) : string =
        "[" + (effects |> List.map encode |> String.concat ",") + "]"

    // ─── the decoder ────────────────────────────────────────────────────────

    let private refuse (cls: RefusalClass) (detail: string) : Result<'T, ClientEffectRefusal> =
        Error { Class = cls; Detail = detail }

    let private tryMember (name: string) (value: JVal) : JVal option =
        match value with
        | JObj fields -> fields |> List.tryFind (fst >> (=) name) |> Option.map snd
        | _ -> None

    let private requireString (name: string) (value: JVal) : Result<string, ClientEffectRefusal> =
        match tryMember name value with
        | Some(JStr s) -> Ok s
        | Some _ -> refuse RefusalClass.MissingMember ("member '" + name + "' is not a string")
        | None -> refuse RefusalClass.MissingMember ("required member '" + name + "' is absent")

    /// A member that names something a client acts on (a route, a node, a url,
    /// a file name, a token) and `Confirm`'s prompt: present and empty is
    /// refused on the class an absent one is, because an empty name names
    /// nothing.
    let private requireNonEmpty (name: string) (value: JVal) : Result<string, ClientEffectRefusal> =
        requireString name value
        |> Result.bind (fun text ->
            if text = "" then
                refuse RefusalClass.MissingMember ("member '" + name + "' is empty")
            else
                Ok text)

    /// Refuse any member the arm does not declare. Stricter than a must-ignore
    /// rule, deliberately: the document is untrusted, and a member some later
    /// version might honour is refused rather than carried.
    let private declaredOnly (allowed: string list) (value: JVal) : Result<unit, ClientEffectRefusal> =
        match value with
        | JObj fields ->
            match fields |> List.map fst |> List.filter (fun k -> not (List.contains k allowed)) with
            | [] -> Ok()
            | extra -> refuse RefusalClass.UndeclaredMember ("undeclared member(s): " + String.concat ", " extra)
        | _ -> refuse RefusalClass.MissingMember "expected an object"

    /// The readable file-body encodings, a closed set.
    let readEncodings: string list = [ "Text"; "Base64"; "DataUrl" ]

    /// Decode one parsed `client-effect` document. Parsing the text, and the
    /// limits a host applies while it does, are the host's: this reads a value
    /// Core's reader has already produced.
    ///
    /// `encode (decode v)` reproduces the document's bytes for every document
    /// the encoder can write. An explicit `"target":"Self"` is accepted (a
    /// declared member holding a declared value) and re-encodes without it,
    /// because `Self` is never written.
    let decode (value: JVal) : Result<ClientEffect, ClientEffectRefusal> =
        let one read name ctor =
            declaredOnly [ "kind"; name ] value
            |> Result.bind (fun () -> read name value)
            |> Result.map ctor

        match value with
        | JObj _ ->
            match tryMember "kind" value with
            | None -> refuse RefusalClass.MissingMember "required member 'kind' is absent"
            | Some(JStr kind) ->
                match kind with
                | "Navigate" ->
                    declaredOnly [ "kind"; "route"; "target" ] value
                    |> Result.bind (fun () -> requireNonEmpty "route" value)
                    |> Result.bind (fun route ->
                        match tryMember "target" value with
                        | None
                        | Some(JStr "Self") -> Ok(Navigate(route, NavigateTarget.Self))
                        | Some(JStr "Blank") -> Ok(Navigate(route, NavigateTarget.Blank))
                        | Some(JStr other) ->
                            refuse
                                RefusalClass.UndeclaredMember
                                ("target '" + other + "' is neither 'Self' nor 'Blank'")
                        | Some _ -> refuse RefusalClass.UndeclaredMember "member 'target' is not a string")
                | "PushState" -> one requireNonEmpty "route" PushState
                | "WriteToClipboard" -> one requireString "text" WriteToClipboard
                | "Focus" -> one requireNonEmpty "nodeId" Focus
                | "Download" ->
                    declaredOnly [ "kind"; "url"; "name" ] value
                    |> Result.bind (fun () -> requireNonEmpty "url" value)
                    |> Result.bind (fun url -> requireNonEmpty "name" value |> Result.map (fun name -> url, name))
                    |> Result.map Download
                | "ReadFileBody" ->
                    declaredOnly [ "kind"; "nodeId"; "encoding" ] value
                    |> Result.bind (fun () -> requireNonEmpty "nodeId" value)
                    |> Result.bind (fun nodeId ->
                        requireString "encoding" value
                        |> Result.bind (fun encoding ->
                            if List.contains encoding readEncodings then
                                Ok(nodeId, encoding)
                            else
                                refuse
                                    RefusalClass.UndeclaredMember
                                    ("encoding '" + encoding + "' is not one of the three")))
                    |> Result.map ReadFileBody
                // `Print` declares no members, so refusing every member but the
                // discriminator is the whole decoder.
                | "Print" -> declaredOnly [ "kind" ] value |> Result.map (fun () -> Print)
                | "Confirm" ->
                    declaredOnly [ "kind"; "prompt"; "token" ] value
                    |> Result.bind (fun () -> requireNonEmpty "prompt" value)
                    |> Result.bind (fun prompt ->
                        requireNonEmpty "token" value |> Result.map (fun token -> prompt, token))
                    |> Result.map Confirm
                | other ->
                    refuse RefusalClass.UnknownEffectArm ("'" + other + "' is not an arm of the closed vocabulary")
            | Some _ -> refuse RefusalClass.MissingMember "member 'kind' is not a string"
        | _ -> refuse RefusalClass.MissingMember "expected an object"
