module LoginHandlerTests

open System
open Xunit
open Swensen.Unquote
open FsCheck.FSharp
open FsCheck.Xunit
open Microsoft.Extensions.DependencyInjection
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private setupCode = "K7QX-M2PA"

let private unclaimedMember =
  { sampleMember with
      Credential = PendingSetup(SetupCode.hash setupCode)
  }

let private claimedMember (hash: string) : FamilyMember =
  { unclaimedMember with
      Credential = Password hash
  }

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
  TestHost.setForm ctx [ FormFields.username, "Me"; FormFields.password, "correct" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.home @>

[<Fact>]
let ``loginWithCredentials redirects to / when remember-me is checked`` () =
  let hash = PasswordHashing.hash "correct"

  let claimed = { claimedMember hash with Name = "Me" }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimed ]

  TestHost.setForm
    ctx
    [
      FormFields.username, "Me"
      FormFields.password, "correct"
      FormFields.rememberMe, "on"
    ]

  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.home @>

[<Fact>]
let ``loginWithCredentials returns 401 for wrong password`` () =
  let hash = PasswordHashing.hash "correct"

  let claimed = { claimedMember hash with Name = "Me" }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimed ]
  TestHost.setForm ctx [ FormFields.username, "Me"; FormFields.password, "wrong" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials returns 401 for unknown user`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.setForm ctx [ FormFields.username, "Nobody"; FormFields.password, "anything" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials returns 401 for an inactive claimed member, even with the correct password`` () =
  let hash = PasswordHashing.hash "correct"

  let inactive =
    { claimedMember hash with
        Name = "Me"
        IsActive = false
    }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ inactive ]
  TestHost.setForm ctx [ FormFields.username, "Me"; FormFields.password, "correct" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials redirects to claim page for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setForm ctx [ FormFields.username, "Me"; FormFields.password, "" ]
  TestHost.run AuthHandlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.loginMember 1 @>

// A stored name and how it was typed: each letter in random case, padded with spaces or tabs.
let private typedNameGen =
  gen {
    let! name =
      Gen.elements [ 'a' .. 'z' ]
      |> Gen.nonEmptyListOf
      |> Gen.map (List.toArray >> String)

    let! upper = Gen.listOfLength name.Length (Gen.elements [ true; false ])

    let typed =
      Seq.map2 (fun (c: char) up -> if up then Char.ToUpperInvariant c else c) name upper
      |> Seq.toArray
      |> String

    let padding =
      Gen.elements [ ' '; '\t' ] |> Gen.listOf |> Gen.map (List.toArray >> String)

    let! before = padding
    let! after = padding
    return name, before + typed + after
  }

[<Property>]
let ``loginWithCredentials finds the member regardless of case and surrounding whitespace`` () =
  Prop.forAll (Arb.fromGen typedNameGen) (fun (name, typed) ->
    let ctx =
      TestHost.contextWithMembers (repoWith []) [ { unclaimedMember with Name = name } ]

    TestHost.setForm ctx [ FormFields.username, typed; FormFields.password, "" ]
    TestHost.run AuthHandlers.loginWithCredentials ctx

    ctx.Response.Headers.Location.ToString() = Routes.loginMember 1)

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
        IsActive = false
    }

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

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, setupCode
      FormFields.password, "correct-horse"
      FormFields.passwordConfirm, "correct-horse"
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let saved = memberRepo.GetById(1) |> Option.get
  test <@ FamilyMember.isClaimed saved @>

  test
    <@
      match saved.Credential with
      | Password hash -> PasswordHashing.verify "correct-horse" hash
      | _ -> false
    @>

[<Fact>]
let ``loginSubmit rejects a wrong setup code with 401 and leaves the account unclaimed`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, "K7QX-M2PB"
      FormFields.password, "correct-horse"
      FormFields.passwordConfirm, "correct-horse"
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 401 @>
  test <@ (TestHost.readBody ctx).Contains "Invalid setup code" @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  test <@ not (FamilyMember.isClaimed (memberRepo.GetById(1) |> Option.get)) @>

[<Fact>]
let ``loginSubmit refuses to claim a member without a setup code`` () =
  let repo = repoWith []

  let ctx =
    TestHost.contextWithMembers
      repo
      [
        { sampleMember with
            Credential = NoCredential
        }
      ]

  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, ""
      FormFields.password, "correct-horse"
      FormFields.passwordConfirm, "correct-horse"
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 401 @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  test <@ not (FamilyMember.isClaimed (memberRepo.GetById(1) |> Option.get)) @>

[<Fact>]
let ``loginMember asks an unclaimed member for the setup code`` () =
  let ctx = TestHost.contextWithMembers (repoWith []) [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.run AuthHandlers.loginMember ctx

  test <@ (TestHost.readBody ctx).Contains $"name=\"{FormFields.setupCode}\"" @>

[<Fact>]
let ``loginSubmit rejects empty password for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, setupCode
      FormFields.password, ""
      FormFields.passwordConfirm, ""
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "empty" @>

[<Fact>]
let ``loginSubmit rejects a new password shorter than 8 characters`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, setupCode
      FormFields.password, "1234567"
      FormFields.passwordConfirm, "1234567"
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "at least 8 characters" @>
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  test <@ not (FamilyMember.isClaimed (memberRepo.GetById(1) |> Option.get)) @>

[<Fact>]
let ``loginSubmit accepts a new password of exactly 8 characters`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, setupCode
      FormFields.password, "12345678"
      FormFields.passwordConfirm, "12345678"
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>

[<Fact>]
let ``loginSubmit rejects mismatched confirm for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.setupCode, setupCode
      FormFields.password, "abc"
      FormFields.passwordConfirm, "xyz"
    ]

  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "do not match" @>

[<Fact>]
let ``loginSubmit accepts correct password for claimed member and redirects`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ FormFields.password, "letmein" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.home @>

[<Fact>]
let ``loginSubmit accepts correct password with remember-me checked and redirects`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ FormFields.password, "letmein"; FormFields.rememberMe, "on" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.home @>

[<Fact>]
let ``loginSubmit rejects wrong password for claimed member with 401`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ FormFields.password, "wrongpassword" ]
  TestHost.run AuthHandlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 401 @>
  test <@ (TestHost.readBody ctx).Contains "Incorrect password" @>

[<Fact>]
let ``loginSubmit returns 403 for inactive member`` () =
  let hash = PasswordHashing.hash "secret"

  let inactive =
    { claimedMember hash with
        IsActive = false
    }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ inactive ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ FormFields.password, "secret" ]
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
