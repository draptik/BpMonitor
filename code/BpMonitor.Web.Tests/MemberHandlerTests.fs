module MemberHandlerTests

open System
open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private adminMember (id: int) (name: string) : FamilyMember =
  {
    Id = id
    Name = name
    IsAdmin = true
    IsActive = true
    Credential = NoCredential
    Goal = GoalRange.defaults
    Language = English
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue
  }

/// The setup code shown on the page, read from its dedicated element.
let private shownSetupCode (body: string) =
  Regex.Match(body, "class=\"setup-code\">([^<]+)<").Groups[1].Value

let private storedSetupCodeHash (ctx: Microsoft.AspNetCore.Http.HttpContext) (name: string) =
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  match (memberRepo.GetAll() |> List.find (fun m -> m.Name = name)).Credential with
  | PendingSetup hash -> Some hash
  | _ -> None

[<Fact>]
let ``createMember with IsAdmin checkbox persists an admin member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm ctx [ FormFields.name, "Alice"; FormFields.isAdmin, "on" ]
  TestHost.run MemberHandlers.createMember ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let added = memberRepo.GetAll() |> List.find (fun m -> m.Name = "Alice")
  test <@ added.IsAdmin = true @>
  test <@ added.IsActive = true @>

[<Fact>]
let ``createMember without IsAdmin checkbox persists a non-admin member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm ctx [ FormFields.name, "Bob" ]
  TestHost.run MemberHandlers.createMember ctx

  test <@ ctx.Response.StatusCode = 200 @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let added = memberRepo.GetAll() |> List.find (fun m -> m.Name = "Bob")
  test <@ added.IsAdmin = false @>

[<Fact>]
let ``createMember rejects a name another member already has, ignoring case`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Bart" ]

  TestHost.setForm ctx [ FormFields.name, "bart" ]
  TestHost.run MemberHandlers.createMember ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "already" @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  test <@ memberRepo.GetAll().Length = 1 @>

[<Fact>]
let ``createMember shows the new member's setup code once, without caching`` () =
  let ctx = TestHost.context (repoWith [])

  TestHost.setForm ctx [ FormFields.name, "Alice" ]
  TestHost.run MemberHandlers.createMember ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ ctx.Response.Headers.CacheControl.ToString() = "no-store" @>
  test <@ shownSetupCode (TestHost.readBody ctx) <> "" @>

[<Fact>]
let ``createMember stores the hash of the setup code it shows`` () =
  let ctx = TestHost.context (repoWith [])

  TestHost.setForm ctx [ FormFields.name, "Alice" ]
  TestHost.run MemberHandlers.createMember ctx

  let code = shownSetupCode (TestHost.readBody ctx)
  let stored = storedSetupCodeHash ctx "Alice"
  test <@ stored |> Option.exists (SetupCode.verify code) @>

[<Fact>]
let ``resetPassword shows a new setup code and stores its hash`` () =
  let ctx =
    TestHost.contextWithMembers
      (repoWith [])
      [
        adminMember 1 "Me"
        { adminMember 2 "Bart" with
            Credential = Password "hashed"
        }
      ]

  TestHost.setRouteId ctx 2
  TestHost.run MemberHandlers.resetPassword ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ ctx.Response.Headers.CacheControl.ToString() = "no-store" @>
  let code = shownSetupCode (TestHost.readBody ctx)
  let stored = storedSetupCodeHash ctx "Bart"
  test <@ stored |> Option.exists (SetupCode.verify code) @>

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

  TestHost.setForm
    ctx
    [
      FormFields.name, "Myself"
      FormFields.isAdmin, "on"
      FormFields.isActive, "on"
    ]

  TestHost.run MemberHandlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.members @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let updated = memberRepo.GetAll() |> List.exactlyOne
  test <@ updated.Name = "Myself" @>

[<Fact>]
let ``updateMember rejects renaming a member to another member's name`` () =
  let repo = repoWith []

  let ctx =
    TestHost.contextWithMembers repo [ adminMember 1 "Me"; adminMember 2 "Alice" ]

  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.name, "ALICE"
      FormFields.isAdmin, "on"
      FormFields.isActive, "on"
    ]

  TestHost.run MemberHandlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "already" @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  test <@ (memberRepo.GetById 1).Value.Name = "Me" @>

[<Fact>]
let ``updateMember rejects demoting the last active admin with 422`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1

  // Uncheck both IsAdmin and IsActive — would leave no active admin.
  TestHost.setForm ctx [ FormFields.name, "Me" ]
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
  TestHost.setForm ctx [ FormFields.name, "Me"; FormFields.isActive, "on" ]
  TestHost.run MemberHandlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
