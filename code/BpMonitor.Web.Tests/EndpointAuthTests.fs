module EndpointAuthTests

open Microsoft.AspNetCore.Http
open Xunit
open Swensen.Unquote
open Falco
open BpMonitor.Web
open HandlerTestHelpers

let private anonymousRoutes =
  set
    [
      "GET", Routes.health
      "GET", Routes.login
      "POST", Routes.login
      "GET", "/login/{id:int}"
      "POST", "/login/{id:int}"
      "POST", Routes.logout
    ]

let private routes =
  [
    for e in Endpoints.all do
      for verb, handler in e.Handlers -> sprintf "%A" verb, e.Pattern, handler
  ]

let private run handler (ctx: HttpContext) =
  TestHost.run handler ctx
  ctx

[<Fact>]
let ``every route outside the anonymous allowlist redirects an unauthenticated request to login`` () =
  let unprotected =
    routes
    |> List.filter (fun (verb, pattern, _) -> not (anonymousRoutes.Contains(verb, pattern)))
    |> List.filter (fun (_, _, handler) ->
      let ctx = run handler (TestHost.contextUnauthenticated (repoWith []))

      ctx.Response.StatusCode <> 302
      || ctx.Response.Headers.Location.ToString() <> Routes.login)
    |> List.map (fun (verb, pattern, _) -> $"{verb} {pattern}")

  test <@ unprotected = [] @>

[<Fact>]
let ``every members route returns 403 for a signed-in non-admin`` () =
  let nonAdmin = { sampleMember with IsAdmin = false }

  let memberRoutes =
    routes |> List.filter (fun (_, pattern, _) -> pattern.StartsWith Routes.members)

  let reachable =
    memberRoutes
    |> List.filter (fun (_, _, handler) ->
      (run handler (TestHost.contextWithMembers (repoWith []) [ nonAdmin ])).Response.StatusCode
      <> 403)
    |> List.map (fun (verb, pattern, _) -> $"{verb} {pattern}")

  test <@ not memberRoutes.IsEmpty @>
  test <@ reachable = [] @>
