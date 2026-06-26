module VersionTests

open Xunit
open Swensen.Unquote
open BpMonitor.Web

[<Theory>]
[<InlineData(null)>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("1.0.0+abc123")>]
let ``parse returns dev for None and sentinel values`` (raw: string) =
  test <@ Version.parse (Option.ofObj raw) = "dev" @>

[<Theory>]
[<InlineData("1.0.0")>]
[<InlineData("0.1.14")>]
[<InlineData("0.1.14+abc123")>]
let ``parse returns the version unchanged`` (v: string) = test <@ Version.parse (Some v) = v @>

[<Fact>]
let ``releaseUrl returns None for dev`` () =
  test <@ Version.releaseUrl "dev" = None @>

[<Fact>]
let ``releaseUrl returns GitHub releases URL for stamped version`` () =
  let expected = Some "https://github.com/draptik/BpMonitor/releases/tag/v0.1.14"
  test <@ Version.releaseUrl "0.1.14" = expected @>
