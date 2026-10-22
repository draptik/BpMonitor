module ConfigTests

open System.Collections.Generic
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Configuration
open BpMonitor.Core
open BpMonitor.Web

let private configWith (pairs: (string * string) list) : IConfiguration =
  ConfigurationBuilder().AddInMemoryCollection(pairs |> List.map (fun (k, v) -> KeyValuePair(k, v))).Build()

[<Fact>]
let ``readRememberMeDays defaults to 30 when unset`` () =
  let config = configWith []
  test <@ Config.readRememberMeDays config = 30 @>

[<Fact>]
let ``readRememberMeDays parses a configured value`` () =
  let config = configWith [ "BpMonitor:RememberMeDays", "7" ]
  test <@ Config.readRememberMeDays config = 7 @>

[<Fact>]
let ``readRememberMeDays falls back to 30 on garbage`` () =
  let config = configWith [ "BpMonitor:RememberMeDays", "not-a-number" ]
  test <@ Config.readRememberMeDays config = 30 @>

[<Fact>]
let ``readRememberMeDays clamps values above the 400-day browser cap`` () =
  let config = configWith [ "BpMonitor:RememberMeDays", "9999" ]
  test <@ Config.readRememberMeDays config = 400 @>

[<Fact>]
let ``readRememberMeDays clamps non-positive values up to 1`` () =
  let config = configWith [ "BpMonitor:RememberMeDays", "0" ]
  test <@ Config.readRememberMeDays config = 1 @>

[<Fact>]
let ``readDefaultLanguage defaults to English when unset`` () =
  let config = configWith []
  test <@ Config.readDefaultLanguage config = English @>

[<Fact>]
let ``readDefaultLanguage parses a configured language code`` () =
  let config = configWith [ "BpMonitor:DefaultLanguage", "de" ]
  test <@ Config.readDefaultLanguage config = German @>

[<Fact>]
let ``readDefaultLanguage falls back to English on an unsupported code`` () =
  let config = configWith [ "BpMonitor:DefaultLanguage", "fr" ]
  test <@ Config.readDefaultLanguage config = English @>

[<Fact>]
let ``readMinPasswordLength defaults to 8 when unset`` () =
  let config = configWith []
  test <@ Config.readMinPasswordLength config = 8 @>

[<Fact>]
let ``readMinPasswordLength parses a configured value`` () =
  let config = configWith [ "BpMonitor:MinPasswordLength", "1" ]
  test <@ Config.readMinPasswordLength config = 1 @>

[<Fact>]
let ``readMinPasswordLength clamps values below 1 to 1`` () =
  let config = configWith [ "BpMonitor:MinPasswordLength", "0" ]
  test <@ Config.readMinPasswordLength config = 1 @>
