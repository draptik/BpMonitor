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
