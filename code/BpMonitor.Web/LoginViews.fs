namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for unauthenticated login pages.
module LoginViews =
  /// Login page: username + password form.
  let loginPage (s: LocalizedStrings) (errors: string list) : XmlNode =
    ViewLayout.loginLayout
      s
      $"{s.Login.PageTitle} — BpMonitor"
      [ Elem.h2 [] [ Text.raw s.Login.SignIn ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action Routes.login; Attr.class' "stacked" ]
          [ Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' FormFields.username ] [ Text.raw s.Shell.Name ]
                Elem.input
                  [ Attr.type' "text"
                    Attr.id FormFields.username
                    Attr.name FormFields.username
                    Attr.create "autofocus" "autofocus"
                    Attr.create "autocomplete" "username" ] ]
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' FormFields.password ] [ Text.raw s.Login.Password ]
                Elem.input
                  [ Attr.type' "password"
                    Attr.id FormFields.password
                    Attr.name FormFields.password
                    Attr.create "autocomplete" "current-password" ] ]
            Elem.label
              [ Attr.for' FormFields.rememberMe ]
              [ Elem.input
                  [ Attr.type' "checkbox"
                    Attr.id FormFields.rememberMe
                    Attr.name FormFields.rememberMe ]
                Text.raw $" {s.Login.RememberMe}" ]
            Elem.div [ Attr.class' "actions" ] [ Elem.button [ Attr.type' "submit" ] [ Text.raw s.Login.SignIn ] ] ] ]

  /// Login form for a specific member. Shows a claim form (password + confirm) for
  /// unclaimed accounts, or a simple password form for claimed ones.
  let loginMember (s: LocalizedStrings) (m: FamilyMember) (errors: string list) : XmlNode =
    let isClaimed = FamilyMember.isClaimed m

    let passwordFields =
      if isClaimed then
        // Claimed: single password field
        [ Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' FormFields.password ] [ Text.raw s.Login.Password ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id FormFields.password
                  Attr.name FormFields.password
                  Attr.create "autofocus" "autofocus"
                  Attr.create "autocomplete" "current-password" ] ] ]
      else
        // Unclaimed: set password + confirm
        [ Elem.p [ Attr.class' "claim-hint" ] [ Text.raw s.Login.ClaimHint ]
          Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' FormFields.password ] [ Text.raw s.Login.NewPassword ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id FormFields.password
                  Attr.name FormFields.password
                  Attr.create "autofocus" "autofocus"
                  Attr.create "autocomplete" "new-password" ] ]
          Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' FormFields.passwordConfirm ] [ Text.raw s.Login.ConfirmPassword ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id FormFields.passwordConfirm
                  Attr.name FormFields.passwordConfirm
                  Attr.create "autocomplete" "new-password" ] ] ]

    ViewLayout.loginLayout
      s
      $"{s.Login.LoginAs m.Name} — BpMonitor"
      [ Elem.h2 [] [ Text.enc (s.Login.LoginAs m.Name) ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action (Routes.loginMember m.Id) ]
          (passwordFields
           @ [ Elem.label
                 [ Attr.for' FormFields.rememberMe ]
                 [ Elem.input
                     [ Attr.type' "checkbox"
                       Attr.id FormFields.rememberMe
                       Attr.name FormFields.rememberMe ]
                   Text.raw $" {s.Login.RememberMe}" ]
               Elem.div
                 [ Attr.class' "actions" ]
                 [ Elem.button
                     [ Attr.type' "submit" ]
                     [ Text.raw (if isClaimed then s.Login.Login else s.Login.ClaimAccount) ]
                   Elem.a
                     [ Attr.href Routes.login; Attr.role "button"; Attr.class' "secondary outline" ]
                     [ Text.raw s.Shell.Back ] ] ]) ]
