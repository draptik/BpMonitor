module AuthCombinatorsTests

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Xunit
open Swensen.Unquote
open BpMonitor.Web
open HandlerTestHelpers

let private ok: HttpContext -> Task =
  fun ctx ->
    ctx.Response.StatusCode <- 200
    ctx.Response.WriteAsync("reached")

let private nonAdminMember = { sampleMember with IsAdmin = false }

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
