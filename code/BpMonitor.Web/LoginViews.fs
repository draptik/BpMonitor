namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for unauthenticated login pages.
module LoginViews =
  /// Login page: username + password form.
  let loginPage (errors: string list) : XmlNode =
    ViewLayout.loginLayout
      "Login — BpMonitor"
      [ Elem.h2 [] [ Text.raw "Sign in" ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action Routes.login; Attr.class' "stacked" ]
          [ Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' FormFields.username ] [ Text.raw "Name" ]
                Elem.input
                  [ Attr.type' "text"
                    Attr.id FormFields.username
                    Attr.name FormFields.username
                    Attr.create "autofocus" "autofocus"
                    Attr.create "autocomplete" "username" ] ]
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' FormFields.password ] [ Text.raw "Password" ]
                Elem.input
                  [ Attr.type' "password"
                    Attr.id FormFields.password
                    Attr.name FormFields.password
                    Attr.create "autocomplete" "current-password" ] ]
            Elem.div [ Attr.class' "actions" ] [ Elem.button [ Attr.type' "submit" ] [ Text.raw "Sign in" ] ] ] ]

  /// Login form for a specific member. Shows a claim form (password + confirm) for
  /// unclaimed accounts, or a simple password form for claimed ones.
  let loginMember (m: FamilyMember) (errors: string list) : XmlNode =
    let isClaimed = FamilyMember.isClaimed m

    let passwordFields =
      if isClaimed then
        // Claimed: single password field
        [ Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' FormFields.password ] [ Text.raw "Password" ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id FormFields.password
                  Attr.name FormFields.password
                  Attr.create "autofocus" "autofocus"
                  Attr.create "autocomplete" "current-password" ] ] ]
      else
        // Unclaimed: set password + confirm
        [ Elem.p
            [ Attr.class' "claim-hint" ]
            [ Text.raw "This account hasn't been claimed yet. Choose a password to activate it." ]
          Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' FormFields.password ] [ Text.raw "New password" ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id FormFields.password
                  Attr.name FormFields.password
                  Attr.create "autofocus" "autofocus"
                  Attr.create "autocomplete" "new-password" ] ]
          Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' FormFields.passwordConfirm ] [ Text.raw "Confirm password" ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id FormFields.passwordConfirm
                  Attr.name FormFields.passwordConfirm
                  Attr.create "autocomplete" "new-password" ] ] ]

    ViewLayout.loginLayout
      $"Login as {m.Name} — BpMonitor"
      [ Elem.h2 [] [ Text.enc $"Login as {m.Name}" ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action $"/login/{m.Id}" ]
          (passwordFields
           @ [ Elem.div
                 [ Attr.class' "actions" ]
                 [ Elem.button [ Attr.type' "submit" ] [ Text.raw (if isClaimed then "Login" else "Claim account") ]
                   Elem.a
                     [ Attr.href Routes.login; Attr.role "button"; Attr.class' "secondary outline" ]
                     [ Text.raw "Back" ] ] ]) ]
