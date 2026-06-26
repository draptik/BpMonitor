module MemberViewTests

open System
open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web
open ViewTestHelpers

[<Fact>]
let ``members page renders Admin and Active columns and Edit link`` () =
  let otherMember =
    { Id = 2
      Name = "Alice"
      IsAdmin = false
      IsActive = true
      PasswordHash = None
      Goal = GoalRange.defaults
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html =
    renderHtml (MemberViews.members [ defaultMember; otherMember ] defaultMember [])

  test <@ html.Contains "Admin" @>
  test <@ html.Contains "Active" @>
  test <@ html.Contains $"href=\"{Routes.memberEdit 1}\"" @>
  test <@ html.Contains $"href=\"{Routes.memberEdit 2}\"" @>

[<Fact>]
let ``members page shows claimed/unclaimed badge`` () =
  let claimed =
    { defaultMember with
        PasswordHash = Some "somehash" }

  let unclaimed =
    { Id = 2
      Name = "Alice"
      IsAdmin = false
      IsActive = true
      PasswordHash = None
      Goal = GoalRange.defaults
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html = renderHtml (MemberViews.members [ claimed; unclaimed ] claimed [])

  test <@ html.Contains "Claimed" @>
  test <@ html.Contains "Unclaimed" @>

[<Fact>]
let ``members page shows reset-password button`` () =
  let html = renderHtml (MemberViews.members [ defaultMember ] defaultMember [])
  test <@ html.Contains "reset-password" @>

[<Fact>]
let ``members page does NOT show Switch button`` () =
  let html = renderHtml (MemberViews.members [ defaultMember ] defaultMember [])
  test <@ not (html.Contains "/members/switch") @>

[<Fact>]
let ``memberForm prefills name and reflects IsAdmin and IsActive`` () =
  let m =
    { Id = 3
      Name = "Bob"
      IsAdmin = true
      IsActive = false
      PasswordHash = None
      Goal = GoalRange.defaults
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html =
    renderHtml (MemberViews.memberForm Routes.members "Me" true "Edit member" (Routes.memberUpdate 3) [] m)

  test <@ html.Contains "value=\"Bob\"" @>
  test <@ html.Contains $"action=\"{Routes.memberUpdate 3}\"" @>
  // IsAdmin checked → checked attribute present
  test <@ html.Contains "checked" @>

[<Fact>]
let ``memberForm renders errors`` () =
  let html =
    renderHtml (
      MemberViews.memberForm
        Routes.members
        "Me"
        true
        "Edit member"
        (Routes.memberUpdate 3)
        [ "At least one member must be an active admin" ]
        defaultMember
    )

  test <@ html.Contains "active admin" @>
