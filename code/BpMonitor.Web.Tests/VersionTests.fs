module VersionTests

open Xunit
open Swensen.Unquote
open BpMonitor.Web

[<Fact>]
let ``parse None returns dev`` () = test <@ Version.parse None = "dev" @>

[<Fact>]
let ``parse empty string returns dev`` () =
  test <@ Version.parse (Some "") = "dev" @>

[<Fact>]
let ``parse whitespace returns dev`` () =
  test <@ Version.parse (Some "   ") = "dev" @>

[<Fact>]
let ``parse default 1.0.0 returns dev`` () =
  test <@ Version.parse (Some "1.0.0") = "dev" @>

[<Fact>]
let ``parse real version returns it unchanged`` () =
  test <@ Version.parse (Some "0.1.14") = "0.1.14" @>

[<Fact>]
let ``parse preserves build metadata after plus`` () =
  test <@ Version.parse (Some "0.1.14+abc123") = "0.1.14+abc123" @>

[<Fact>]
let ``parse 1.0.0 with sha is not treated as dev`` () =
  test <@ Version.parse (Some "1.0.0+abc123") = "1.0.0+abc123" @>

[<Fact>]
let ``releaseUrl returns None for dev`` () =
  test <@ Version.releaseUrl "dev" = None @>

[<Fact>]
let ``releaseUrl returns GitHub releases URL for stamped version`` () =
  let expected = Some "https://github.com/draptik/BpMonitor/releases/tag/v0.1.14"
  test <@ Version.releaseUrl "0.1.14" = expected @>
