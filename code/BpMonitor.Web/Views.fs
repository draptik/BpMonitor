namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views (Falco.Markup). Pure functions of their inputs so
/// they can be unit-/snapshot-tested without a running host.
module Views =
  /// Page shell: shared <head>, vendored htmx, stylesheet and hx-boosted body.
  let private layout (title: string) (content: XmlNode list) : XmlNode =
    // Runs once on initial load; survives hx-boost navigations because it lives in <head>.
    let themeScript =
      """(function(){
  var t=localStorage.getItem('theme')||(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light');
  document.documentElement.setAttribute('data-theme',t);
})();
window.toggleTheme=function(){
  var h=document.documentElement,n=h.getAttribute('data-theme')==='dark'?'light':'dark';
  h.setAttribute('data-theme',n);
  localStorage.setItem('theme',n);
  var b=document.getElementById('theme-toggle');
  if(b)b.textContent=n==='dark'?'Light':'Dark';
};"""
    // Re-runs on every body render (initial + hx-boost swaps) to sync the button label.
    let labelScript =
      "(function(){var b=document.getElementById('theme-toggle');if(b)b.textContent=document.documentElement.getAttribute('data-theme')==='dark'?'Light':'Dark';})();"

    Elem.html
      [ Attr.lang "en" ]
      [ Elem.head
          []
          [ Elem.meta [ Attr.charset "utf-8" ]
            Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
            Elem.script [] [ Text.raw themeScript ]
            Elem.link
              [ Attr.rel "stylesheet"
                Attr.href "https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css" ]
            Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ]
            Elem.script [ Attr.src "/htmx.min.js" ] [] ]
        Elem.body
          [ Attr.create "hx-boost" "true" ]
          [ Elem.nav
              [ Attr.class' "container" ]
              [ Elem.ul [] [ Elem.li [] [ Elem.strong [] [ Text.raw "BpMonitor" ] ] ]
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
            Elem.script [] [ Text.raw labelScript ] ] ]

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

  /// Dashboard: chart (isolated in an iframe) above the readings table.
  let dashboard (readings: BloodPressureReading list) : XmlNode =
    layout
      "BpMonitor"
      [ Elem.h1 [] [ Text.raw "Blood Pressure" ]
        Elem.p [] [ Elem.a [ Attr.href "/readings/new"; Attr.role "button" ] [ Text.raw "Add reading" ] ]
        Elem.details
          []
          [ Elem.summary [ Attr.class' "chart-toggle" ] [ Text.raw "Blood Pressure Graph" ]
            Elem.iframe [ Attr.src "/chart"; Attr.class' "chart"; Attr.title "Blood Pressure History" ] [] ]
        readingsTable readings ]

  /// Shared add/edit form. `action` is the POST target; `errors` are rendered
  /// above the fields when re-displaying after a failed submit.
  let readingForm (title: string) (action: string) (errors: string list) (m: Binding.FormModel) : XmlNode =
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
                Elem.a [ Attr.href "/"; Attr.role "button"; Attr.class' "secondary" ] [ Text.raw "Cancel" ] ] ] ]
