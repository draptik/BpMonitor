module MemberHandlerTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private adminMember (id: int) (name: string) : FamilyMember =
  { Id = id
    Name = name
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    Goal = GoalRange.defaults
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``createMember with IsAdmin checkbox persists an admin member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm ctx [ "Name", "Alice"; "IsAdmin", "on" ]
  TestHost.run MemberHandlers.createMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let added = memberRepo.GetAll() |> List.find (fun m -> m.Name = "Alice")
  test <@ added.IsAdmin = true @>
  test <@ added.IsActive = true @>

[<Fact>]
let ``createMember without IsAdmin checkbox persists a non-admin member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm ctx [ "Name", "Bob" ]
  TestHost.run MemberHandlers.createMember ctx

  test <@ ctx.Response.StatusCode = 302 @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let added = memberRepo.GetAll() |> List.find (fun m -> m.Name = "Bob")
  test <@ added.IsAdmin = false @>

[<Fact>]
let ``editMember prefills the form for an existing member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1
  TestHost.run MemberHandlers.editMember ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "value=\"Me\"" @>

[<Fact>]
let ``editMember returns 404 for an unknown id`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 999
  TestHost.run MemberHandlers.editMember ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``updateMember saves changes and redirects to /members`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1

  TestHost.setForm ctx [ "Name", "Myself"; "IsAdmin", "on"; "IsActive", "on" ]
  TestHost.run MemberHandlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/members" @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let updated = memberRepo.GetAll() |> List.exactlyOne
  test <@ updated.Name = "Myself" @>

[<Fact>]
let ``updateMember rejects demoting the last active admin with 422`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1

  // Uncheck both IsAdmin and IsActive — would leave no active admin.
  TestHost.setForm ctx [ "Name", "Me" ]
  TestHost.run MemberHandlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "active admin" @>

[<Fact>]
let ``updateMember allows demoting one admin when another active admin exists`` () =
  let repo = repoWith []

  let ctx =
    TestHost.contextWithMembers repo [ adminMember 1 "Me"; adminMember 2 "Alice" ]

  TestHost.setRouteId ctx 1

  // Demote member 1 to non-admin; member 2 remains active admin → invariant holds.
  TestHost.setForm ctx [ "Name", "Me"; "IsActive", "on" ]
  TestHost.run MemberHandlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
