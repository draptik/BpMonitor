module LoginHandlerTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private unclaimedMember: FamilyMember =
  { Id = 1
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    Goal = GoalRange.defaults
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private claimedMember (hash: string) : FamilyMember =
  { unclaimedMember with
      PasswordHash = Some hash }

[<Fact>]
let ``loginPage returns 200 with sign-in form`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.run AuthHandlers.loginPage ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "Sign in" @>

[<Fact>]
let ``loginWithCredentials redirects to / for correct credentials`` () =
  let hash = PasswordHashing.hash "correct"

  let claimed = { claimedMember hash with Name = "Me" }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimed ]
  TestHost.setForm ctx [ "Username", "Me"; "Password", "correct" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/" @>

[<Fact>]
let ``loginWithCredentials returns 401 for wrong password`` () =
  let hash = PasswordHashing.hash "correct"

  let claimed = { claimedMember hash with Name = "Me" }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimed ]
  TestHost.setForm ctx [ "Username", "Me"; "Password", "wrong" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials returns 401 for unknown user`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.setForm ctx [ "Username", "Nobody"; "Password", "anything" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials redirects to claim page for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setForm ctx [ "Username", "Me"; "Password", "" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/login/1" @>

[<Fact>]
let ``loginMember returns 200 for an existing active member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.run AuthHandlers.loginMember ctx

  test <@ ctx.Response.StatusCode = 200 @>

[<Fact>]
let ``loginMember returns 404 for unknown member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 999
  TestHost.run AuthHandlers.loginMember ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``loginMember returns 403 for inactive member`` () =
  let inactive =
    { unclaimedMember with
        IsActive = false }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ inactive ]
  TestHost.setRouteId ctx 1
  TestHost.run AuthHandlers.loginMember ctx

  test <@ ctx.Response.StatusCode = 403 @>

[<Fact>]
let ``loginSubmit claims unclaimed member and sets password hash`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "correct-horse"; "PasswordConfirm", "correct-horse" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let saved = memberRepo.GetById(1) |> Option.get
  test <@ FamilyMember.isClaimed saved @>
  test <@ PasswordHashing.verify "correct-horse" (saved.PasswordHash |> Option.get) @>

[<Fact>]
let ``loginSubmit rejects empty password for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", ""; "PasswordConfirm", "" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "empty" @>

[<Fact>]
let ``loginSubmit rejects mismatched confirm for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "abc"; "PasswordConfirm", "xyz" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "do not match" @>

[<Fact>]
let ``loginSubmit accepts correct password for claimed member and redirects`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "letmein" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/" @>

[<Fact>]
let ``loginSubmit rejects wrong password for claimed member with 401`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "wrongpassword" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 401 @>
  test <@ (TestHost.readBody ctx).Contains "Incorrect password" @>

[<Fact>]
let ``loginSubmit returns 403 for inactive member`` () =
  let hash = PasswordHashing.hash "secret"

  let inactive =
    { claimedMember hash with
        IsActive = false }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ inactive ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "secret" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 403 @>

[<Fact>]
let ``resetPassword sets member to unclaimed`` () =
  let hash = PasswordHashing.hash "original"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.run MemberHandlers.resetPassword ctx

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let saved = memberRepo.GetById(1) |> Option.get
  test <@ not (FamilyMember.isClaimed saved) @>
