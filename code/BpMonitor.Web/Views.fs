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
                    navLink active "/history" "History" ]
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
  let history (readings: BloodPressureReading list) : XmlNode =
    layout
      "/history"
      "History"
      [ Elem.h1 [] [ Text.raw "History" ]
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
