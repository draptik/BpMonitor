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
      Language = English
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html =
    renderHtml (MemberViews.members s [ defaultMember; otherMember ] defaultMember [])

  test <@ html.Contains "Admin" @>
  test <@ html.Contains "Active" @>
  test <@ html.Contains $"href=\"{Routes.memberEdit 1}\"" @>
  test <@ html.Contains $"href=\"{Routes.memberEdit 2}\"" @>

[<Fact>]
let ``languageSection lists every supported language as a select option`` () =
  let html = renderHtml (Elem.div [] (MemberViews.languageSection s English))

  test <@ html.Contains "<select" @>
  test <@ html.Contains "value=\"en\"" @>
  test <@ html.Contains "value=\"de\"" @>
  test <@ html.Contains "English" @>
  test <@ html.Contains "Deutsch" @>

[<Fact>]
let ``languageSection's form opts out of hx-boost so <html lang> updates on submit`` () =
  let html = renderHtml (Elem.div [] (MemberViews.languageSection s English))
  test <@ html.Contains "hx-boost=\"false\"" @>

[<Fact>]
let ``languageSection marks the member's current language as selected`` () =
  let html = renderHtml (Elem.div [] (MemberViews.languageSection s German))

  test <@ html.Contains "value=\"de\" selected" @>
  test <@ not (html.Contains "value=\"en\" selected") @>

[<Fact>]
let ``goalRangeSection wraps the section in a collapsible details element`` () =
  let html =
    renderHtml (
      Elem.div
        []
        (MemberViews.goalRangeSection
          s
          []
          { Binding.SysMin = "90"
            Binding.SysMax = "140"
            Binding.DiaMin = "60"
            Binding.DiaMax = "90" })
    )

  test <@ html.Contains "<details" @>
  test <@ html.Contains "data-persist-key=\"settings-goal-range\"" @>
  test <@ html.Contains "<summary>" @>

[<Fact>]
let ``members page renders Edit as a button, matching Reset password's style`` () =
  let html = renderHtml (MemberViews.members s [ defaultMember ] defaultMember [])

  test <@ html.Contains $"<a href=\"{Routes.memberEdit 1}\" role=\"button\" class=\"outline secondary\">Edit</a>" @>

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
      Language = English
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html = renderHtml (MemberViews.members s [ claimed; unclaimed ] claimed [])

  test <@ html.Contains "Claimed" @>
  test <@ html.Contains "Unclaimed" @>

[<Fact>]
let ``members page shows reset-password button`` () =
  let html = renderHtml (MemberViews.members s [ defaultMember ] defaultMember [])
  test <@ html.Contains "reset-password" @>

[<Fact>]
let ``members page does NOT show Switch button`` () =
  let html = renderHtml (MemberViews.members s [ defaultMember ] defaultMember [])
  test <@ not (html.Contains "/members/switch") @>

[<Fact>]
let ``members page wraps its content in the dense-page density scope`` () =
  let html = renderHtml (MemberViews.members s [ defaultMember ] defaultMember [])
  test <@ html.Contains "dense-page" @>

[<Fact>]
let ``memberForm prefills name and reflects IsAdmin and IsActive`` () =
  let m =
    { Id = 3
      Name = "Bob"
      IsAdmin = true
      IsActive = false
      PasswordHash = None
      Goal = GoalRange.defaults
      Language = English
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html =
    renderHtml (MemberViews.memberForm s Routes.members "Me" true "Edit member" (Routes.memberUpdate 3) [] m)

  test <@ html.Contains "value=\"Bob\"" @>
  test <@ html.Contains $"action=\"{Routes.memberUpdate 3}\"" @>
  // IsAdmin checked → checked attribute present
  test <@ html.Contains "checked" @>

[<Fact>]
let ``memberForm renders errors`` () =
  let html =
    renderHtml (
      MemberViews.memberForm
        s
        Routes.members
        "Me"
        true
        "Edit member"
        (Routes.memberUpdate 3)
        [ "At least one member must be an active admin" ]
        defaultMember
    )

  test <@ html.Contains "active admin" @>
