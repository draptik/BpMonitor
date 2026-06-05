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

  /// Page shell: shared <head>, vendored htmx, stylesheet and hx-boosted body.
  /// `active` is the route of the current page so the nav can highlight it.
  let private layout (active: string) (title: string) (content: XmlNode list) : XmlNode =
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
                    navLink active "/members" "Members" ]
                Elem.ul
                  []
                  [ Elem.li
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

  let private errorBox (errors: string list) : XmlNode =
    match errors with
    | [] -> Text.raw ""
    | _ ->
      Elem.div
        [ Attr.class' "errors"; Attr.role "alert" ]
        [ Elem.ul [] (errors |> List.map (fun e -> Elem.li [] [ Text.enc e ])) ]

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
  let landing: XmlNode =
    layout
      "/"
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
      "History"
      [ Elem.h1 [] [ Text.raw $"History — {activeMember.Name}" ]
        Elem.details
          []
          [ Elem.summary [ Attr.class' "chart-toggle" ] [ Text.raw "Blood Pressure Graph" ]
            Elem.iframe [ Attr.src "/chart"; Attr.class' "chart"; Attr.title "Blood Pressure History" ] [] ]
        readingsTable readings ]

  /// Shared add/edit form. `action` is the POST target; `errors` are rendered
  /// above the fields when re-displaying after a failed submit.
  let readingForm
    (active: string)
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
    let badge (text: string) =
      Elem.span [ Attr.class' "badge" ] [ Text.raw text ]

    let memberRow (m: FamilyMember) =
      let isCurrent = m.Id = active.Id

      Elem.tr
        []
        [ Elem.td [] [ Text.enc m.Name ]
          Elem.td [] [ if m.IsAdmin then badge "Admin" else Text.raw "—" ]
          Elem.td [] [ if m.IsActive then badge "Active" else Text.raw "—" ]
          Elem.td
            [ Attr.style "display:flex; gap:0.5rem; align-items:center; flex-wrap:wrap" ]
            [ if isCurrent then
                Elem.span [ Attr.class' "current-member" ] [ Text.raw "Current" ]
              else
                Elem.form
                  [ Attr.method "post"; Attr.action "/members/switch" ]
                  [ Elem.input [ Attr.type' "hidden"; Attr.name "MemberId"; Attr.value (string m.Id) ]
                    Elem.input [ Attr.type' "hidden"; Attr.name "ReturnUrl"; Attr.value "/members" ]
                    Elem.button [ Attr.type' "submit"; Attr.class' "outline" ] [ Text.raw "Switch" ] ]
              Elem.a [ Attr.href $"/members/{m.Id}/edit"; Attr.class' "outline" ] [ Text.raw "Edit" ] ] ]

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
  let memberForm (active: string) (title: string) (action: string) (errors: string list) (m: FamilyMember) : XmlNode =
    let checkedAttr isChecked =
      if isChecked then
        [ Attr.type' "checkbox"; Attr.create "checked" "checked" ]
      else
        [ Attr.type' "checkbox" ]

    layout
      active
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

  /// Members page: list of family members with Switch/Edit buttons and an add form.
  let members (allMembers: FamilyMember list) (active: FamilyMember) : XmlNode =
    layout "/members" "Family Members" (Elem.h1 [] [ Text.raw "Family Members" ] :: membersList allMembers active None)

  /// Members page rendered with a validation error message.
  let membersWithError (allMembers: FamilyMember list) (active: FamilyMember) (error: string) : XmlNode =
    layout
      "/members"
      "Family Members"
      (Elem.h1 [] [ Text.raw "Family Members" ]
       :: membersList allMembers active (Some error))
