module AuthCombinatorsTests

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private ok: HttpContext -> Task =
  fun ctx ->
    ctx.Response.StatusCode <- 200
    ctx.Response.WriteAsync("reached")

let private nonAdminMember = { sampleMember with IsAdmin = false }

let private echoMember: FamilyMember -> HttpContext -> Task =
  fun m ctx ->
    ctx.Response.StatusCode <- 200
    ctx.Response.WriteAsync(m.Name)

let private echoMemberAndId: FamilyMember -> int -> HttpContext -> Task =
  fun m id ctx ->
    ctx.Response.StatusCode <- 200
    ctx.Response.WriteAsync($"{m.Name}:{id}")

[<Fact>]
let ``protect redirects an unauthenticated request to login`` () =
  let ctx = TestHost.contextUnauthenticated (repoWith [])
  TestHost.run (AuthHandlers.protect ok) ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.login @>

[<Fact>]
let ``protect invokes the handler for an authenticated request`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.run (AuthHandlers.protect ok) ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ TestHost.readBody ctx = "reached" @>

[<Fact>]
let ``protectAdmin redirects an unauthenticated request to login`` () =
  let ctx = TestHost.contextUnauthenticated (repoWith [])
  TestHost.run (AuthHandlers.protectAdmin ok) ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.login @>

[<Fact>]
let ``protectAdmin returns 403 for an authenticated non-admin request`` () =
  let ctx = TestHost.contextWithMembers (repoWith []) [ nonAdminMember ]
  TestHost.run (AuthHandlers.protectAdmin ok) ctx

  test <@ ctx.Response.StatusCode = 403 @>

[<Fact>]
let ``protectAdmin invokes the handler for an authenticated admin request`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.run (AuthHandlers.protectAdmin ok) ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ TestHost.readBody ctx = "reached" @>

[<Fact>]
let ``withMember redirects to login when the principal's member id no longer resolves`` () =
  // Simulates a stale cookie after the signed-in member's account was deleted.
  let ctx = TestHost.contextWithMembers (repoWith []) [ sampleMember ]
  ctx.User <- TestHost.buildPrincipal { sampleMember with Id = 999 }
  TestHost.run (AuthHandlers.withMember echoMember) ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.login @>

[<Fact>]
let ``withMemberAndRouteId redirects to login when the principal's member id no longer resolves`` () =
  let ctx = TestHost.contextWithMembers (repoWith []) [ sampleMember ]
  ctx.User <- TestHost.buildPrincipal { sampleMember with Id = 999 }
  TestHost.setRouteId ctx sampleMember.Id
  TestHost.run (AuthHandlers.withMemberAndRouteId "test" echoMemberAndId) ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.login @>

[<Fact>]
let ``withMemberAndRouteId returns 400 for a non-integer id, without redirecting`` () =
  let ctx = TestHost.context (repoWith [])
  ctx.Request.RouteValues["id"] <- box "not-a-number"
  TestHost.run (AuthHandlers.withMemberAndRouteId "test" echoMemberAndId) ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``withMemberAndRouteId invokes the handler with both the member and the route id`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.setRouteId ctx 42
  TestHost.run (AuthHandlers.withMemberAndRouteId "test" echoMemberAndId) ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ TestHost.readBody ctx = $"{sampleMember.Name}:42" @>
