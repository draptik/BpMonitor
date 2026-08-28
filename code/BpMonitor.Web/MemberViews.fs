namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for family-member management pages.
module MemberViews =
  let private membersList
    (s: LocalizedStrings)
    (allMembers: FamilyMember list)
    (active: FamilyMember)
    (errors: string list)
    : XmlNode list =
    let badge (text: string) (cls: string) =
      Elem.span [ Attr.class' cls ] [ Text.raw text ]

    let memberRow (m: FamilyMember) =
      let isCurrent = m.Id = active.Id

      Elem.tr
        []
        [ Elem.td [] [ Text.enc m.Name ]
          Elem.td
            []
            [ if m.IsAdmin then
                badge s.Member.AdminBadge "badge"
              else
                Text.raw s.Member.NoneBadge ]
          Elem.td
            []
            [ if m.IsActive then
                badge s.Member.ActiveBadge "badge"
              else
                Text.raw s.Member.NoneBadge ]
          Elem.td
            []
            [ if FamilyMember.isClaimed m then
                badge s.Member.ClaimedBadge "badge badge-claimed"
              else
                badge s.Member.UnclaimedBadge "badge badge-unclaimed" ]
          Elem.td
            [ Attr.class' "member-actions" ]
            [ if isCurrent then
                Elem.span [ Attr.class' "current-member" ] [ Text.raw s.Member.You ]
              Elem.a
                [ Attr.href (Routes.memberEdit m.Id)
                  Attr.role "button"
                  Attr.class' "outline secondary" ]
                [ Text.raw s.Shell.Edit ]
              ViewLayout.inlinePostButton (Routes.memberResetPassword m.Id) s.Member.ResetPassword ] ]

    [ yield ViewLayout.errorBox errors
      yield
        Elem.table
          []
          [ Elem.thead
              []
              [ Elem.tr
                  []
                  [ Elem.th [] [ Text.raw s.Shell.Name ]
                    Elem.th [] [ Text.raw s.Member.AdminHeader ]
                    Elem.th [] [ Text.raw s.Member.ActiveHeader ]
                    Elem.th [] [ Text.raw s.Member.PasswordHeader ]
                    Elem.th [] [ Text.raw "" ] ] ]
            Elem.tbody [] (allMembers |> List.map memberRow) ]
      yield Elem.h2 [] [ Text.raw s.Member.AddFamilyMember ]
      yield
        Elem.form
          [ Attr.method "post"; Attr.action Routes.members; Attr.class' "stacked" ]
          [ Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' FormFields.name ] [ Text.raw s.Shell.Name ]
                Elem.input [ Attr.type' "text"; Attr.id FormFields.name; Attr.name FormFields.name ] ]
            Elem.label
              [ Attr.for' FormFields.isAdmin ]
              [ Elem.input
                  [ Attr.type' "checkbox"
                    Attr.id FormFields.isAdmin
                    Attr.name FormFields.isAdmin ]
                Text.raw s.Member.AdminCheckboxLabel ]
            Elem.button [ Attr.type' "submit" ] [ Text.raw s.Member.AddMember ] ] ]

  /// Shared add/edit form for family members. `action` is the POST target; `errors`
  /// are rendered above the fields when re-displaying after a failed submit.
  let memberForm
    (s: LocalizedStrings)
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
      s
      active
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ ViewLayout.field s.Shell.Name FormFields.name m.Name "text"
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label
                  [ Attr.for' FormFields.isAdmin ]
                  [ Elem.input (
                      checkedAttr m.IsAdmin
                      @ [ Attr.id FormFields.isAdmin; Attr.name FormFields.isAdmin ]
                    )
                    Text.raw s.Member.AdminCheckboxLabel ] ]
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label
                  [ Attr.for' FormFields.isActive ]
                  [ Elem.input (
                      checkedAttr m.IsActive
                      @ [ Attr.id FormFields.isActive; Attr.name FormFields.isActive ]
                    )
                    Text.raw s.Member.ActiveCheckboxLabel ] ]
            ViewLayout.formActions s Routes.members ] ]

  /// Self-service language picker fragment: submits a `<select>` of every `Language.all`
  /// entry (each labeled by its own `Language.nativeName`) to `/settings/language`.
  let languageSection (s: LocalizedStrings) (current: Language) : XmlNode list =
    let option (lang: Language) =
      let attrs =
        [ yield Attr.value (Language.code lang)
          if lang = current then
            yield Attr.create "selected" "selected" ]

      Elem.option attrs [ Text.raw (Language.nativeName lang) ]

    [ Elem.details
        [ Attr.class' "settings-section"
          Attr.create "open" ""
          Attr.create "data-persist-key" "settings-language" ]
        [ Elem.summary [] [ Elem.h2 [] [ Text.raw s.Member.LanguageTitle ] ]
          // hx-boost="false": submitting must be a full page load, not an htmx body-only
          // swap — otherwise <html lang> (set at the outer document level) never updates.
          Elem.form
            [ Attr.method "post"
              Attr.action Routes.settingsLanguage
              Attr.create "hx-boost" "false" ]
            [ Elem.div
                [ Attr.class' "field" ]
                [ Elem.label [ Attr.for' FormFields.language ] [ Text.raw s.Member.LanguageTitle ]
                  Elem.select
                    [ Attr.id FormFields.language; Attr.name FormFields.language ]
                    (Language.all |> List.map option) ]
              ViewLayout.formActions s Routes.settings ] ] ]

  /// Self-service goal-range settings fragment: a fragment (not a full page) so `/settings`
  /// can compose it with `MedicationViews.medicationsSection` under one shell.
  let goalRangeSection
    (s: LocalizedStrings)
    (errors: string list)
    (goalInput: Binding.GoalRangeFormModel)
    : XmlNode list =
    [ Elem.details
        [ Attr.class' "settings-section"
          Attr.create "open" ""
          Attr.create "data-persist-key" "settings-goal-range" ]
        [ Elem.summary [] [ Elem.h2 [] [ Text.raw s.Member.GoalRangeTitle ] ]
          ViewLayout.errorBox errors
          Elem.form
            [ Attr.method "post"; Attr.action Routes.settings ]
            [ ViewLayout.field s.Member.SystolicMin FormFields.systolicGoalMin goalInput.SysMin "number"
              ViewLayout.field s.Member.SystolicMax FormFields.systolicGoalMax goalInput.SysMax "number"
              ViewLayout.field s.Member.DiastolicMin FormFields.diastolicGoalMin goalInput.DiaMin "number"
              ViewLayout.field s.Member.DiastolicMax FormFields.diastolicGoalMax goalInput.DiaMax "number"
              ViewLayout.formActions s Routes.history ] ] ]

  /// Members page: list of family members with Edit/Reset-password buttons and an add form.
  /// Pass non-empty `errors` to show validation errors above the add form.
  let members
    (s: LocalizedStrings)
    (allMembers: FamilyMember list)
    (active: FamilyMember)
    (errors: string list)
    : XmlNode =
    ViewLayout.layout
      s
      Routes.members
      active.Name
      active.IsAdmin
      s.Member.FamilyMembersTitle
      [ Elem.div
          [ Attr.class' "dense-page" ]
          (Elem.h1 [] [ Text.raw s.Member.FamilyMembersTitle ]
           :: membersList s allMembers active errors) ]
