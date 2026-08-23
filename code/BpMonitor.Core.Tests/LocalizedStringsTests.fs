module LocalizedStringsTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

/// Reflection-walks a LocalizedStrings value so this test needs no update when a field is added.
module private StringsIntrospection =
  open Microsoft.FSharp.Reflection

  let rec private collect (path: string) (value: obj) (t: Type) : (string * string) list =
    if t = typeof<string> then
      [ path, value :?> string ]
    elif FSharpType.IsRecord t then
      FSharpType.GetRecordFields t
      |> Array.toList
      |> List.collect (fun p -> collect $"{path}.{p.Name}" (p.GetValue value) p.PropertyType)
    elif FSharpType.IsFunction t then
      let domain, range = FSharpType.GetFunctionElements t

      let arg: obj =
        if domain = typeof<int> then box 1
        elif domain = typeof<string> then box "x"
        else null

      let result = t.GetMethod("Invoke", [| domain |]).Invoke(value, [| arg |])
      collect path result range
    else
      []

  /// Every leaf string in a LocalizedStrings value, paired with its field path
  /// (function fields are invoked with placeholder arguments first).
  let allStrings (s: LocalizedStrings) : (string * string) list =
    collect "" (box s) typeof<LocalizedStrings>

/// Field paths whose value is legitimately identical across every language: units,
/// literal format patterns shown to the user, and the app's own brand name.
let private sharedAcrossLanguages =
  set
    [ ".Reading.LandingTitle" // "BpMonitor" — brand name
      ".Reading.TimestampHint" // "yyyy-MM-dd HH:mm" — literal input format
      ".Medication.StartDateHint" // "dd.mm.yyyy" — literal input format
      ".Medication.EndDateHint" // "dd.mm.yyyy" — literal input format
      ".Table.MmHg" // unit
      ".Table.Bpm" // unit
      ".Member.NoneBadge" // "—"
      ".Member.AdminBadge" // "Admin" — used as a loanword in German too
      ".Member.AdminHeader" // "Admin"
      ".Member.AdminCheckboxLabel" // " Admin"
      ".Trend.TrendsTitle" // "Trends" — used as a loanword in German too
      ".Shell.NavTrends" // "Trends" — same
      ".Shell.Name" // "Name" — identical cognate in both languages
      ".Medication.Optional" // "Optional" — used as a loanword in German too
      ".Medication.StartHeader" // "Start" — identical cognate in both languages
      ".Trend.MonthOfYear" // false positive: placeholder arg month=1 abbreviates to "Jan" in both
      ".Trend.Year" ] // numeric only

[<Fact>]
let ``en has no null or blank string anywhere in the record`` () =
  let blanks =
    StringsIntrospection.allStrings LocalizedStrings.en
    |> List.filter (fun (_, s) -> String.IsNullOrWhiteSpace s)

  test <@ blanks = [] @>

[<Fact>]
let ``de has no null or blank string anywhere in the record`` () =
  let blanks =
    StringsIntrospection.allStrings LocalizedStrings.de
    |> List.filter (fun (_, s) -> String.IsNullOrWhiteSpace s)

  test <@ blanks = [] @>

[<Fact>]
let ``forLanguage resolves every supported language to a LocalizedStrings value`` () =
  for lang in Language.all do
    test <@ (LocalizedStrings.forLanguage lang).Language = lang @>

[<Fact>]
let ``de differs from en on every field not in the shared-vocabulary allowlist`` () =
  let en = StringsIntrospection.allStrings LocalizedStrings.en |> Map.ofList

  let de =
    StringsIntrospection.allStrings (LocalizedStrings.forLanguage German)
    |> Map.ofList

  let unexpectedlyIdentical =
    en
    |> Map.toList
    |> List.filter (fun (path, enValue) -> not (sharedAcrossLanguages.Contains path) && de[path] = enValue)
    |> List.map fst

  test <@ unexpectedlyIdentical = [] @>
