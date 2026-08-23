namespace BpMonitor.Core

/// A UI language supported by the app. See `Strings` for the translated text itself.
type Language =
  | English
  | German

module Language =
  let code =
    function
    | English -> "en"
    | German -> "de"

  /// Every supported language, in the order offered to users (e.g. the settings picker).
  let all = [ English; German ]

  let defaultLanguage = English

  /// The language's own name for itself, for a language picker — never translated.
  let nativeName =
    function
    | English -> "English"
    | German -> "Deutsch"

  /// Recognizes an ISO 639-1 code or a region-qualified culture tag (e.g. "de", "de-DE"),
  /// case-insensitively, by its base language. Returns None for anything unsupported.
  let tryParse (s: string) : Language option =
    let baseCode =
      match s.IndexOf('-') with
      | -1 -> s
      | i -> s.Substring(0, i)

    match baseCode.ToLowerInvariant() with
    | "en" -> Some English
    | "de" -> Some German
    | _ -> None
