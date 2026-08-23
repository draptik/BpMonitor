module StringsTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

/// Reflection-walks a Strings value so this test needs no update when a field is added.
module private StringsIntrospection =
  open Microsoft.FSharp.Reflection

  let rec private collect (value: obj) (t: Type) : string list =
    if t = typeof<string> then
      [ value :?> string ]
    elif FSharpType.IsRecord t then
      FSharpType.GetRecordFields t
      |> Array.toList
      |> List.collect (fun p -> collect (p.GetValue value) p.PropertyType)
    elif FSharpType.IsFunction t then
      let domain, range = FSharpType.GetFunctionElements t

      let arg: obj =
        if domain = typeof<int> then box 1
        elif domain = typeof<string> then box "x"
        else null

      let result = t.GetMethod("Invoke", [| domain |]).Invoke(value, [| arg |])
      collect result range
    else
      []

  let allStrings (s: Strings) : string list = collect (box s) typeof<Strings>

[<Fact>]
let ``en has no null or blank string anywhere in the record`` () =
  let blanks =
    StringsIntrospection.allStrings Strings.en
    |> List.filter (fun s -> String.IsNullOrWhiteSpace s)

  test <@ blanks = [] @>

[<Fact>]
let ``forLanguage resolves every supported language to a Strings value`` () =
  for lang in Language.all do
    test <@ (Strings.forLanguage lang).Language = lang @>
