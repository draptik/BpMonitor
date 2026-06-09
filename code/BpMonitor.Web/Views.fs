namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views (Falco.Markup). Pure functions of their inputs so
/// they can be unit-/snapshot-tested without a running host.
module Views =
  /// A single nav link, marked `aria-current="page"` (which Pico styles as active)
  /// when its href matches the page's `active` route.
  let private navLink (active: string) (href: string) (label: string) : XmlNode =
    let attrs =
      if href = active then
        [ Attr.href href; Attr.create "aria-current" "page" ]
      else
        [ Attr.href href ]

    Elem.li [] [ Elem.a attrs [ Text.raw label ] ]

  /// Page shell for authenticated pages: shared <head>, nav bar with logged-in member
  /// name + logout, and hx-boosted body.
  let private layout
    (active: string)
    (memberName: string)
    (isAdmin: bool)
    (title: string)
    (content: XmlNode list)
    : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ Elem.head
          []
          [ Elem.meta [ Attr.charset "utf-8" ]
            Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
            // Runs once on initial load; survives hx-boost navigations because it lives in <head>.
            // No defer/async — render-blocking prevents flash of wrong theme (FOUC).
            Elem.script [ Attr.src "/theme.js" ] []
            Elem.link
              [ Attr.rel "stylesheet"
                Attr.href "https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css" ]
            Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ]
            Elem.script [ Attr.src "/htmx.min.js" ] [] ]
        Elem.body
          [ Attr.create "hx-boost" "true" ]
          [ Elem.nav
              [ Attr.class' "container" ]
              [ Elem.ul
                  []
                  [ Elem.li [] [ Elem.strong [] [ Text.raw "BpMonitor" ] ]
                    navLink active "/" "Home"
                    navLink active "/add" "Add"
                    navLink active "/history" "History"
                    navLink active "/trends" "Trends"
                    if isAdmin then
                      navLink active "/members" "Members" ]
                Elem.ul
                  []
                  [ Elem.li [] [ Elem.span [ Attr.class' "nav-member-name" ] [ Text.enc memberName ] ]
                    Elem.li
                      []
                      [ Elem.form
                          [ Attr.method "post"; Attr.action "/logout"; Attr.class' "inline" ]
                          [ Elem.button [ Attr.type' "submit"; Attr.class' "outline secondary" ] [ Text.raw "Logout" ] ] ]
                    Elem.li
                      []
                      [ Elem.button
                          [ Attr.id "theme-toggle"
                            Attr.class' "outline secondary"
                            Attr.create "onclick" "toggleTheme()" ]
                          [] ] ] ]
            Elem.main [ Attr.class' "container" ] content
            Elem.footer
              [ Attr.class' "container" ]
              [ Elem.small
                  []
                  (let v = Version.current

                   match Version.releaseUrl v with
                   | Some url -> [ Text.raw "BpMonitor "; Elem.a [ Attr.href url ] [ Text.raw $"v{v}" ] ]
                   | None -> [ Text.raw $"BpMonitor {v}" ]) ]
            // Re-runs on every body render (initial + hx-boost swaps) to sync the button label.
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  /// Minimal page shell for unauthenticated pages (login). No nav, no logout.
  let private loginLayout (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ Elem.head
          []
          [ Elem.meta [ Attr.charset "utf-8" ]
            Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
            Elem.script [ Attr.src "/theme.js" ] []
            Elem.link
              [ Attr.rel "stylesheet"
                Attr.href "https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css" ]
            Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ] ]
        Elem.body
          [ Attr.create "hx-boost" "false" ]
          [ Elem.nav
              [ Attr.class' "container" ]
              [ Elem.ul [] []
                Elem.ul
                  []
                  [ Elem.li
                      []
                      [ Elem.button
                          [ Attr.id "theme-toggle"
                            Attr.class' "outline secondary"
                            Attr.create "onclick" "toggleTheme()" ]
                          [] ] ] ]
            Elem.main
              [ Attr.class' "container login-container" ]
              ([ Elem.header
                   []
                   [ Elem.h1 [] [ Text.raw "BpMonitor" ]
                     Elem.p [] [ Text.raw "Blood pressure tracker" ] ] ]
               @ content)
            Elem.footer
              [ Attr.class' "container" ]
              [ Elem.small
                  []
                  (let v = Version.current

                   match Version.releaseUrl v with
                   | Some url -> [ Text.raw "BpMonitor "; Elem.a [ Attr.href url ] [ Text.raw $"v{v}" ] ]
                   | None -> [ Text.raw $"BpMonitor {v}" ]) ]
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  let private errorBox (errors: string list) : XmlNode =
    match errors with
    | [] -> Text.raw ""
    | _ ->
      Elem.div
        [ Attr.class' "errors"; Attr.role "alert" ]
        [ Elem.ul [] (errors |> List.map (fun e -> Elem.li [] [ Text.enc e ])) ]

  // ---------------------------------------------------------------------------
  // Login views
  // ---------------------------------------------------------------------------

  /// Login page: username + password form.
  let loginPage (errors: string list) : XmlNode =
    loginLayout
      "Login — BpMonitor"
      [ Elem.h2 [] [ Text.raw "Sign in" ]
        errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action "/login"; Attr.class' "stacked" ]
          [ Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' "Username" ] [ Text.raw "Name" ]
                Elem.input
                  [ Attr.type' "text"
                    Attr.id "Username"
                    Attr.name "Username"
                    Attr.create "autofocus" "autofocus"
                    Attr.create "autocomplete" "username" ] ]
            Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' "Password" ] [ Text.raw "Password" ]
                Elem.input
                  [ Attr.type' "password"
                    Attr.id "Password"
                    Attr.name "Password"
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
            [ Elem.label [ Attr.for' "Password" ] [ Text.raw "Password" ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id "Password"
                  Attr.name "Password"
                  Attr.create "autofocus" "autofocus"
                  Attr.create "autocomplete" "current-password" ] ] ]
      else
        // Unclaimed: set password + confirm
        [ Elem.p
            [ Attr.class' "claim-hint" ]
            [ Text.raw "This account hasn't been claimed yet. Choose a password to activate it." ]
          Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' "Password" ] [ Text.raw "New password" ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id "Password"
                  Attr.name "Password"
                  Attr.create "autofocus" "autofocus"
                  Attr.create "autocomplete" "new-password" ] ]
          Elem.div
            [ Attr.class' "field" ]
            [ Elem.label [ Attr.for' "PasswordConfirm" ] [ Text.raw "Confirm password" ]
              Elem.input
                [ Attr.type' "password"
                  Attr.id "PasswordConfirm"
                  Attr.name "PasswordConfirm"
                  Attr.create "autocomplete" "new-password" ] ] ]

    loginLayout
      $"Login as {m.Name} — BpMonitor"
      [ Elem.h2 [] [ Text.enc $"Login as {m.Name}" ]
        errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action $"/login/{m.Id}" ]
          (passwordFields
           @ [ Elem.div
                 [ Attr.class' "actions" ]
                 [ Elem.button [ Attr.type' "submit" ] [ Text.raw (if isClaimed then "Login" else "Claim account") ]
                   Elem.a
                     [ Attr.href "/login"; Attr.role "button"; Attr.class' "secondary outline" ]
                     [ Text.raw "Back" ] ] ]) ]

  // ---------------------------------------------------------------------------
  // App views (all need the logged-in member for the nav)
  // ---------------------------------------------------------------------------

  /// The readings table; wrapped in an id'd container so it can be targeted for
  /// partial swaps later.
  let readingsTable (readings: BloodPressureReading list) : XmlNode =
    let header =
      Elem.thead
        []
        [ Elem.tr
            []
            [ Elem.th [ Attr.class' "col-timestamp" ] [ Text.raw "Timestamp" ]
              Elem.th [ Attr.class' "col-center" ] [ Text.raw "Systolic" ]
              Elem.th [ Attr.class' "col-center" ] [ Text.raw "Diastolic" ]
              Elem.th [ Attr.class' "col-center" ] [ Text.raw "Heart Rate" ]
              Elem.th [] [ Text.raw "Comment" ]
              Elem.th [] [ Text.raw "" ] ] ]

    let row (r: BloodPressureReading) =
      Elem.tr
        []
        [ Elem.td [ Attr.class' "col-timestamp" ] [ Text.enc (Formats.formatLocal r.Timestamp) ]
          Elem.td [ Attr.class' "col-center" ] [ Text.enc (string r.Systolic) ]
          Elem.td [ Attr.class' "col-center" ] [ Text.enc (string r.Diastolic) ]
          Elem.td [ Attr.class' "col-center" ] [ Text.enc (string r.HeartRate) ]
          Elem.td [] [ Text.enc (r.Comments |> Option.defaultValue "") ]
          Elem.td [] [ Elem.a [ Attr.href $"/readings/{r.Id}/edit" ] [ Text.raw "Edit" ] ] ]

    Elem.div [ Attr.id "readings" ] [ Elem.table [] [ header; Elem.tbody [] (readings |> List.map row) ] ]

  /// Landing page: a simple hub linking to the app's main destinations.
  let landing (m: FamilyMember) : XmlNode =
    layout
      "/"
      m.Name
      m.IsAdmin
      "BpMonitor"
      [ Elem.h1 [] [ Text.raw "BpMonitor" ]
        Elem.p [] [ Text.raw "Track and review your blood pressure readings." ]
        Elem.div
          [ Attr.class' "actions" ]
          [ Elem.a [ Attr.href "/add"; Attr.role "button" ] [ Text.raw "Add reading" ]
            Elem.a [ Attr.href "/history"; Attr.role "button" ] [ Text.raw "History" ] ] ]

  /// History: chart (isolated in an iframe) above the readings table.
  let history (activeMember: FamilyMember) (readings: BloodPressureReading list) : XmlNode =
    layout
      "/history"
      activeMember.Name
      activeMember.IsAdmin
      "History"
      [ Elem.h1 [] [ Text.raw $"History — {activeMember.Name}" ]
        Elem.details
          []
          [ Elem.summary [ Attr.class' "chart-toggle" ] [ Text.raw "Blood Pressure Graph" ]
            Elem.iframe
              [ Attr.create "data-chart-src" "/chart"
                Attr.class' "chart"
                Attr.title "Blood Pressure History" ]
              [] ]
        readingsTable readings ]

  /// Shared add/edit form. `action` is the POST target; `errors` are rendered
  /// above the fields when re-displaying after a failed submit.
  let readingForm
    (active: string)
    (memberName: string)
    (isAdmin: bool)
    (title: string)
    (action: string)
    (errors: string list)
    (m: Binding.FormModel)
    : XmlNode =
    let field (labelText: string) (name: string) (value: string) (inputType: string) =
      Elem.div
        [ Attr.class' "field" ]
        [ Elem.label [ Attr.for' name ] [ Text.raw labelText ]
          Elem.input [ Attr.type' inputType; Attr.id name; Attr.name name; Attr.value value ] ]

    let fieldWithHint (labelText: string) (hint: string) (name: string) (value: string) (inputType: string) =
      Elem.div
        [ Attr.class' "field" ]
        [ Elem.label [ Attr.for' name ] [ Text.raw labelText ]
          Elem.small [ Attr.class' "field-hint" ] [ Text.raw hint ]
          Elem.input [ Attr.type' inputType; Attr.id name; Attr.name name; Attr.value value ] ]

    layout
      active
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ fieldWithHint "Timestamp" "yyyy-MM-dd HH:mm" "Timestamp" m.Timestamp "text"
            fieldWithHint "Systolic" "mmHg" "Systolic" m.Systolic "number"
            fieldWithHint "Diastolic" "mmHg" "Diastolic" m.Diastolic "number"
            fieldWithHint "Heart Rate" "bpm" "HeartRate" m.HeartRate "number"
            field "Comment" "Comments" m.Comments "text"
            Elem.div
              [ Attr.class' "actions" ]
              [ Elem.button [ Attr.type' "submit" ] [ Text.raw "Save" ]
                Elem.a [ Attr.href "/history"; Attr.role "button"; Attr.class' "secondary" ] [ Text.raw "Cancel" ] ] ] ]

  let private membersList
    (allMembers: FamilyMember list)
    (active: FamilyMember)
    (errorMsg: string option)
    : XmlNode list =
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
            [ Attr.style "display:flex; gap:0.5rem; align-items:center; flex-wrap:wrap" ]
            [ if isCurrent then
                Elem.span [ Attr.class' "current-member" ] [ Text.raw "You" ]
              Elem.a [ Attr.href $"/members/{m.Id}/edit"; Attr.class' "outline" ] [ Text.raw "Edit" ]
              Elem.form
                [ Attr.method "post"
                  Attr.action $"/members/{m.Id}/reset-password"
                  Attr.class' "inline" ]
                [ Elem.button [ Attr.type' "submit"; Attr.class' "outline secondary" ] [ Text.raw "Reset password" ] ] ] ]

    [ match errorMsg with
      | Some msg -> yield errorBox [ msg ]
      | None -> ()
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
          [ Attr.method "post"; Attr.action "/members"; Attr.class' "stacked" ]
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

    layout
      active
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ Elem.div
              [ Attr.class' "field" ]
              [ Elem.label [ Attr.for' "Name" ] [ Text.raw "Name" ]
                Elem.input [ Attr.type' "text"; Attr.id "Name"; Attr.name "Name"; Attr.value m.Name ] ]
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
            Elem.div
              [ Attr.class' "actions" ]
              [ Elem.button [ Attr.type' "submit" ] [ Text.raw "Save" ]
                Elem.a [ Attr.href "/members"; Attr.role "button"; Attr.class' "secondary" ] [ Text.raw "Cancel" ] ] ] ]

  /// Members page: list of family members with Edit/Reset-password buttons and an add form.
  let members (allMembers: FamilyMember list) (active: FamilyMember) : XmlNode =
    layout
      "/members"
      active.Name
      active.IsAdmin
      "Family Members"
      (Elem.h1 [] [ Text.raw "Family Members" ] :: membersList allMembers active None)

  /// Members page rendered with a validation error message.
  let membersWithError (allMembers: FamilyMember list) (active: FamilyMember) (error: string) : XmlNode =
    layout
      "/members"
      active.Name
      active.IsAdmin
      "Family Members"
      (Elem.h1 [] [ Text.raw "Family Members" ]
       :: membersList allMembers active (Some error))

  // ---------------------------------------------------------------------------
  // Trends views
  // ---------------------------------------------------------------------------

  /// The swappable panel: window-toggle buttons + stats + chart iframe.
  /// Rendered as a fragment for htmx swaps (GET /trends/{days}); also used directly
  /// by the full /trends page so the buttons are always inside the swapped region.
  let trendsPanel (summary: WindowSummary) : XmlNode =
    let windows = [ 7; 14; 30; 90 ]

    let windowButton (days: int) =
      let isActive = days = summary.Days

      let baseAttrs =
        [ Attr.href $"/trends/{days}"
          Attr.role "button"
          Attr.create "hx-get" $"/trends/{days}"
          Attr.create "hx-target" "#trends-panel"
          Attr.create "hx-swap" "outerHTML" ]

      let attrs =
        if isActive then
          baseAttrs @ [ Attr.create "aria-current" "page" ]
        else
          baseAttrs @ [ Attr.class' "outline" ]

      Elem.a attrs [ Text.raw $"{days}d" ]

    let content =
      if summary.Count = 0 then
        [ Elem.p [ Attr.class' "trends-empty" ] [ Text.enc $"No readings in the last {summary.Days} days." ] ]
      else
        let simpleRow (label: string) (value: string) =
          Elem.tr
            []
            [ Elem.th [ Attr.scope "row" ] [ Text.raw label ]
              Elem.td [] [ Text.raw value ] ]

        let statRow (label: string) (unit: string) (avg: int) (mn: int) (mx: int) =
          simpleRow $"{label} ({unit})" $"{avg} (min: {mn}, max: {mx})"

        [ Elem.table
            [ Attr.class' "trends-stats" ]
            [ Elem.tbody
                []
                [ simpleRow "Readings" (string summary.Count)
                  statRow "Avg Systolic" "mmHg" summary.AvgSystolic summary.MinSystolic summary.MaxSystolic
                  statRow "Avg Diastolic" "mmHg" summary.AvgDiastolic summary.MinDiastolic summary.MaxDiastolic
                  statRow "Avg Heart Rate" "bpm" summary.AvgHeartRate summary.MinHeartRate summary.MaxHeartRate ] ]
          Elem.iframe
            [ Attr.create "data-chart-src" $"/chart?window={summary.Days}"
              Attr.class' "chart"
              Attr.title $"Blood Pressure — last {summary.Days} days" ]
            [] ]

    Elem.div
      [ Attr.id "trends-panel" ]
      (Elem.div [ Attr.class' "trends-window-buttons" ] (windows |> List.map windowButton)
       :: content)

  /// The /trends full page. Pre-renders the 30-day panel (including toggle buttons).
  let trends (m: FamilyMember) (summary: WindowSummary) : XmlNode =
    layout "/trends" m.Name m.IsAdmin "Trends" [ Elem.h1 [] [ Text.raw "Trends" ]; trendsPanel summary ]
