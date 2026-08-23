module LanguageTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core

[<Fact>]
let ``code returns the ISO 639-1 code for each language`` () =
  test <@ Language.code English = "en" @>
  test <@ Language.code German = "de" @>

[<Fact>]
let ``defaultLanguage is English`` () =
  test <@ Language.defaultLanguage = English @>

[<Fact>]
let ``all lists every supported language`` () =
  test <@ Language.all = [ English; German ] @>

[<Fact>]
let ``tryParse recognizes exact language codes`` () =
  test <@ Language.tryParse "en" = Some English @>
  test <@ Language.tryParse "de" = Some German @>

[<Fact>]
let ``tryParse recognizes region-qualified culture tags by their base language`` () =
  test <@ Language.tryParse "en-GB" = Some English @>
  test <@ Language.tryParse "de-DE" = Some German @>

[<Fact>]
let ``tryParse is case-insensitive`` () =
  test <@ Language.tryParse "DE" = Some German @>
  test <@ Language.tryParse "En-Us" = Some English @>

[<Fact>]
let ``tryParse returns None for an unsupported or empty code`` () =
  test <@ Language.tryParse "fr" = None @>
  test <@ Language.tryParse "" = None @>

[<Fact>]
let ``nativeName returns the language's own name for itself`` () =
  test <@ Language.nativeName English = "English" @>
  test <@ Language.nativeName German = "Deutsch" @>
