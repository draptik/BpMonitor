namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for reading-related pages (landing, history, add/edit form).
module ReadingViews =
  /// Landing page: a simple hub linking to the app's main destinations.
  let landing (m: FamilyMember) : XmlNode =
    ViewLayout.layout
      "/"
      m.Name
      m.IsAdmin
      "BpMonitor"
      [ Elem.h1 [] [ Text.raw "BpMonitor" ]
        Elem.p [] [ Text.raw "Track and review your blood pressure readings." ]
        Elem.div
          [ Attr.class' "actions" ]
          [ Elem.a [ Attr.href "/add"; Attr.role "button" ] [ Text.raw "Add reading" ]
            Elem.a [ Attr.href Routes.history; Attr.role "button" ] [ Text.raw "History" ] ] ]

  /// History: chart (isolated in an iframe) above the readings table.
  let history (activeMember: FamilyMember) (readings: BloodPressureReading list) : XmlNode =
    ViewLayout.layout
      Routes.history
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
        ViewLayout.readingsTable readings ]

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

    ViewLayout.layout
      active
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        ViewLayout.errorBox errors
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
                Elem.a [ Attr.href Routes.history; Attr.role "button"; Attr.class' "secondary" ] [ Text.raw "Cancel" ] ] ] ]
