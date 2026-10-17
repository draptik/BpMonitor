module HandlerHelpersTests

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private echoId: int -> HttpContext -> Task =
  fun id ctx ->
    ctx.Response.StatusCode <- 200
    ctx.Response.WriteAsync(string id)

let private echoMember: FamilyMember -> HttpContext -> Task =
  fun m ctx ->
    ctx.Response.StatusCode <- 200
    ctx.Response.WriteAsync(m.Name)

[<Fact>]
let ``withRouteId invokes the handler with the parsed id`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.setRouteId ctx 42
  TestHost.run (HandlerHelpers.withRouteId "test" echoId) ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ TestHost.readBody ctx = "42" @>

[<Fact>]
let ``withRouteId returns 400 for a non-integer id`` () =
  let ctx = TestHost.context (repoWith [])
  ctx.Request.RouteValues["id"] <- box "not-a-number"
  TestHost.run (HandlerHelpers.withRouteId "test" echoId) ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``withRouteId returns 400 for a missing id`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.run (HandlerHelpers.withRouteId "test" echoId) ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``withRouteMember invokes the handler with the resolved member`` () =
  let ctx = TestHost.contextWithMembers (repoWith []) [ sampleMember ]
  TestHost.setRouteId ctx sampleMember.Id
  TestHost.run (HandlerHelpers.withRouteMember "test" echoMember) ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ TestHost.readBody ctx = sampleMember.Name @>

[<Fact>]
let ``withRouteMember returns 400 for a non-integer id`` () =
  let ctx = TestHost.contextWithMembers (repoWith []) [ sampleMember ]
  ctx.Request.RouteValues["id"] <- box "not-a-number"
  TestHost.run (HandlerHelpers.withRouteMember "test" echoMember) ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``withRouteMember returns 404 for an unknown member id`` () =
  let ctx = TestHost.contextWithMembers (repoWith []) [ sampleMember ]
  TestHost.setRouteId ctx 999
  TestHost.run (HandlerHelpers.withRouteMember "test" echoMember) ctx

  test <@ ctx.Response.StatusCode = 404 @>

// ── strings: language for unauthenticated pages ──

/// The add-reading title `strings` resolves to for the given cookie and Accept-Language header.
let private resolvedTitle (cookie: string option) (acceptLanguage: string option) =
  let ctx = TestHost.contextUnauthenticated (repoWith [])

  cookie
  |> Option.iter (fun c -> ctx.Request.Headers.Cookie <- $"{HandlerHelpers.languageCookieName}={c}")

  acceptLanguage |> Option.iter (fun h -> ctx.Request.Headers.AcceptLanguage <- h)
  (HandlerHelpers.strings ctx).Reading.AddReadingTitle

let private english = LocalizedStrings.en.Reading.AddReadingTitle
let private german = LocalizedStrings.de.Reading.AddReadingTitle

[<Fact>]
let ``strings falls back to English without a cookie or Accept-Language header`` () =
  test <@ resolvedTitle None None = english @>

[<Fact>]
let ``strings uses the Accept-Language header when there is no language cookie`` () =
  test <@ resolvedTitle None (Some "de-DE") = german @>

[<Fact>]
let ``strings prefers the language cookie over the Accept-Language header`` () =
  test <@ resolvedTitle (Some "en") (Some "de") = english @>

[<Fact>]
let ``strings skips unsupported Accept-Language entries until it finds a supported one`` () =
  test <@ resolvedTitle None (Some "fr-FR, fr;q=0.9, de;q=0.8") = german @>

[<Fact>]
let ``strings picks the Accept-Language entry with the highest q-weight, not the first one`` () =
  test <@ resolvedTitle None (Some "en;q=0.1, de;q=0.9") = german @>

[<Fact>]
let ``strings never picks a language the Accept-Language header marks as not acceptable with q=0`` () =
  test <@ resolvedTitle None (Some "de;q=0, en") = english @>

[<Fact>]
let ``strings keeps header order between Accept-Language entries of equal weight`` () =
  test <@ resolvedTitle None (Some "de, en") = german @>
