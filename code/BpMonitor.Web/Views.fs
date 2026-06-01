namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views (Falco.Markup). Pure functions of their inputs so
/// they can be unit-/snapshot-tested without a running host.
module Views =
  /// Page shell: shared <head>, vendored htmx, stylesheet and hx-boosted body.
  let private layout (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ Elem.head
          []
          [ Elem.meta [ Attr.charset "utf-8" ]
            Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ]
            Elem.script [ Attr.src "/htmx.min.js" ] [] ]
        Elem.body [ Attr.create "hx-boost" "true" ] [ Elem.main [ Attr.class' "container" ] content ] ]

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
            [ Elem.th [] [ Text.raw "Timestamp" ]
              Elem.th [] [ Text.raw "Systolic" ]
              Elem.th [] [ Text.raw "Diastolic" ]
              Elem.th [] [ Text.raw "Heart Rate" ]
              Elem.th [] [ Text.raw "Comments" ]
              Elem.th [] [ Text.raw "" ] ] ]

    let row (r: BloodPressureReading) =
      Elem.tr
        []
        [ Elem.td [] [ Text.enc (Formats.formatLocal r.Timestamp) ]
          Elem.td [] [ Text.enc (string r.Systolic) ]
          Elem.td [] [ Text.enc (string r.Diastolic) ]
          Elem.td [] [ Text.enc (string r.HeartRate) ]
          Elem.td [] [ Text.enc (r.Comments |> Option.defaultValue "") ]
          Elem.td [] [ Elem.a [ Attr.href $"/readings/{r.Id}/edit" ] [ Text.raw "Edit" ] ] ]

    Elem.div [ Attr.id "readings" ] [ Elem.table [] [ header; Elem.tbody [] (readings |> List.map row) ] ]

  /// Dashboard: chart (isolated in an iframe) above the readings table.
  let dashboard (readings: BloodPressureReading list) : XmlNode =
    layout
      "BpMonitor"
      [ Elem.h1 [] [ Text.raw "Blood Pressure" ]
        Elem.p [] [ Elem.a [ Attr.href "/readings/new"; Attr.class' "button" ] [ Text.raw "Add reading" ] ]
        Elem.iframe [ Attr.src "/chart"; Attr.class' "chart"; Attr.title "Blood Pressure History" ] []
        readingsTable readings ]

  /// Shared add/edit form. `action` is the POST target; `errors` are rendered
  /// above the fields when re-displaying after a failed submit.
  let readingForm (title: string) (action: string) (errors: string list) (m: Binding.FormModel) : XmlNode =
    let field (labelText: string) (name: string) (value: string) (inputType: string) =
      Elem.div
        [ Attr.class' "field" ]
        [ Elem.label [ Attr.for' name ] [ Text.raw labelText ]
          Elem.input [ Attr.type' inputType; Attr.id name; Attr.name name; Attr.value value ] ]

    layout
      title
      [ Elem.h1 [] [ Text.raw title ]
        errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ field "Timestamp (yyyy-MM-dd HH:mm)" "Timestamp" m.Timestamp "text"
            field "Systolic" "Systolic" m.Systolic "number"
            field "Diastolic" "Diastolic" m.Diastolic "number"
            field "Heart Rate" "HeartRate" m.HeartRate "number"
            field "Comments" "Comments" m.Comments "text"
            Elem.div
              [ Attr.class' "actions" ]
              [ Elem.button [ Attr.type' "submit" ] [ Text.raw "Save" ]
                Elem.a [ Attr.href "/" ] [ Text.raw "Cancel" ] ] ] ]
