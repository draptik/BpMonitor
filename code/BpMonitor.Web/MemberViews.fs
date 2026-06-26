namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for family-member management pages.
module MemberViews =
  let private membersList (allMembers: FamilyMember list) (active: FamilyMember) (errors: string list) : XmlNode list =
    let badge (text: string) (cls: string) =
      Elem.span [ Attr.class' cls ] [ Text.raw text ]

    let memberRow (m: FamilyMember) =
      let isCurrent = m.Id = active.Id

      Elem.tr
        []
        [ Elem.td [] [ Text.enc m.Name ]
          Elem.td [] [ if m.IsAdmin then badge "Admin" "badge" else Text.raw "—" ]
          Elem.td [] [ if m.IsActive then badge "Active" "badge" else Text.raw "—" ]
          Elem.td
            []
            [ if FamilyMember.isClaimed m then
                badge "Claimed" "badge badge-claimed"
              else
                badge "Unclaimed" "badge badge-unclaimed" ]
          Elem.td
            [ Attr.class' "member-actions" ]
            [ if isCurrent then
                Elem.span [ Attr.class' "current-member" ] [ Text.raw "You" ]
              Elem.a [ Attr.href (Routes.memberEdit m.Id); Attr.class' "outline" ] [ Text.raw "Edit" ]
              Elem.form
                [ Attr.method "post"
                  Attr.action (Routes.memberResetPassword m.Id)
                  Attr.class' "inline" ]
                [ Elem.button [ Attr.type' "submit"; Attr.class' "outline secondary" ] [ Text.raw "Reset password" ] ] ] ]

    [ yield ViewLayout.errorBox errors
      yield
        Elem.table
          []
          [ Elem.thead
              []
              [ Elem.tr
                  []
                  [ Elem.th [] [ Text.raw "Name" ]
                    Elem.th [] [ Text.raw "Admin" ]
                    Elem.th [] [ Text.raw "Active" ]
                    Elem.th [] [ Text.raw "Password" ]
                    Elem.th [] [ Text.raw "" ] ] ]
            Elem.tbody [] (allMembers |> List.map memberRow) ]
      yield Elem.h2 [] [ Text.raw "Add family member" ]
      yield
        Elem.form
          [ Attr.method "post"; Attr.action Routes.members; Attr.class' "stacked" ]
          [ Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' "Name" ] [ Text.raw "Name" ]
                Elem.input [ Attr.type' "text"; Attr.id "Name"; Attr.name "Name" ] ]
            Elem.label
              [ Attr.for' "IsAdmin" ]
              [ Elem.input [ Attr.type' "checkbox"; Attr.id "IsAdmin"; Attr.name "IsAdmin" ]
                Text.raw " Admin" ]
            Elem.button [ Attr.type' "submit" ] [ Text.raw "Add member" ] ] ]

  /// Shared add/edit form for family members. `action` is the POST target; `errors`
  /// are rendered above the fields when re-displaying after a failed submit.
  let memberForm
    (active: string)
    (memberName: string)
    (isAdmin: bool)
    (title: string)
    (action: string)
    (errors: string list)
    (m: FamilyMember)
    : XmlNode =
    let checkedAttr isChecked =
      if isChecked then
        [ Attr.type' "checkbox"; Attr.create "checked" "checked" ]
      else
        [ Attr.type' "checkbox" ]

    ViewLayout.layout
      active
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ ViewLayout.field "Name" "Name" m.Name "text"
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label
                  [ Attr.for' "IsAdmin" ]
                  [ Elem.input (checkedAttr m.IsAdmin @ [ Attr.id "IsAdmin"; Attr.name "IsAdmin" ])
                    Text.raw " Admin" ] ]
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label
                  [ Attr.for' "IsActive" ]
                  [ Elem.input (checkedAttr m.IsActive @ [ Attr.id "IsActive"; Attr.name "IsActive" ])
                    Text.raw " Active" ] ]
            ViewLayout.formActions Routes.members ] ]

  /// Self-service goal-range settings page: lets the logged-in member edit their own
  /// systolic/diastolic goal range, rendered as color-coded bands on their charts.
  /// Field values are raw strings (not a validated GoalRange) so that a failed submit
  /// redisplays exactly what the user typed — mirroring ReadingViews.readingForm's
  /// Binding.FormModel redisplay — instead of falling back to the stale persisted goal.
  let settingsForm
    (memberName: string)
    (isAdmin: bool)
    (errors: string list)
    (sysMin: string)
    (sysMax: string)
    (diaMin: string)
    (diaMax: string)
    : XmlNode =
    ViewLayout.layout
      Routes.settings
      memberName
      isAdmin
      "Goal Range"
      [ Elem.h1 [] [ Text.raw "Goal Range" ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action Routes.settings ]
          [ ViewLayout.field "Systolic min" "SystolicGoalMin" sysMin "number"
            ViewLayout.field "Systolic max" "SystolicGoalMax" sysMax "number"
            ViewLayout.field "Diastolic min" "DiastolicGoalMin" diaMin "number"
            ViewLayout.field "Diastolic max" "DiastolicGoalMax" diaMax "number"
            ViewLayout.formActions Routes.history ] ]

  /// Members page: list of family members with Edit/Reset-password buttons and an add form.
  /// Pass non-empty `errors` to show validation errors above the add form.
  let members (allMembers: FamilyMember list) (active: FamilyMember) (errors: string list) : XmlNode =
    ViewLayout.layout
      Routes.members
      active.Name
      active.IsAdmin
      "Family Members"
      (Elem.h1 [] [ Text.raw "Family Members" ] :: membersList allMembers active errors)
