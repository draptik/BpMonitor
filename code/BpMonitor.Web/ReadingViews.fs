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

  /// History: chart above the readings table.
  let history (activeMember: FamilyMember) (chartHtml: string) (readings: BloodPressureReading list) : XmlNode =
    ViewLayout.layout
      Routes.history
      activeMember.Name
      activeMember.IsAdmin
      "History"
      [ Elem.h1 [] [ Text.raw "History" ]
        Elem.details
          []
          [ Elem.summary [ Attr.class' "chart-toggle" ] [ Text.raw "Blood Pressure Graph" ]
            Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ] ]
        ViewLayout.readingsTable readings ]

  /// Recent: chart of the last 30 days plus three rolling window tables (7/14/30 days).
  let recent
    (activeMember: FamilyMember)
    (chartHtml: string)
    (days7: BloodPressureReading list)
    (days14: BloodPressureReading list)
    (days30: BloodPressureReading list)
    : XmlNode =
    let pill (label: string) (anchor: string) =
      Elem.a [ Attr.href $"#{anchor}"; Attr.role "button"; Attr.class' "outline" ] [ Text.raw label ]

    let section (heading: string) (anchor: string) (readings: BloodPressureReading list) =
      Elem.section [ Attr.id anchor ] [ Elem.h2 [] [ Text.raw heading ]; ViewLayout.readingsTable readings ]

    let valueStrip =
      // The strip lists the same readings as the chart below it (days30).
      let chronological = days30 |> List.sortBy _.Timestamp

      // Each cell is tagged with the same x-label the chart uses for this reading
      // (Charts.fs `seriesOf` formats x as Formats.formatLocal r.Timestamp), so the
      // scrubber script below can match a hovered chart point back to its strip column.
      let row (label: string) (value: BloodPressureReading -> int) =
        Elem.tr
          []
          [ yield Elem.th [ Attr.scope "row"; Attr.class' "value-strip-label" ] [ Text.raw label ]
            for r in chronological ->
              Elem.td
                [ Attr.class' "value-strip-value"
                  Attr.create "data-x" (Formats.formatLocal r.Timestamp) ]
                [ Text.raw (string (value r)) ] ]

      Elem.div
        [ Attr.class' "value-strip" ]
        [ Elem.table [] [ Elem.tbody [] [ row "Systolic" _.Systolic; row "Diastolic" _.Diastolic ] ] ]

    // Fig. 5's scrubber bar (Wegier et al. 2021): the chart's x-axis spike (Charts.fs
    // `recentXAxis`) already draws the moving vertical line; this links it to the value
    // strip by boxing the hovered column. Follows the same poll-until-ready pattern as
    // the chart's own errorBarScript (Charts.fs `errorBarScript`).
    let scrubberScript =
      Text.raw (
        "<script>(function(){"
        + "function setup(){"
        + "var d=document.querySelector('.js-plotly-plot');"
        + "if(!d||!d.on){setTimeout(setup,50);return;}"
        + "d.on('plotly_hover',function(e){"
        + "var x=e.points[0].x;"
        + "document.querySelectorAll('.value-strip td.scrubbed').forEach(function(c){c.classList.remove('scrubbed');});"
        + "document.querySelectorAll('.value-strip td[data-x=\"'+x+'\"]').forEach(function(c){c.classList.add('scrubbed');});"
        + "});"
        + "d.on('plotly_unhover',function(){"
        + "document.querySelectorAll('.value-strip td.scrubbed').forEach(function(c){c.classList.remove('scrubbed');});"
        + "});"
        + "}"
        + "setTimeout(setup,0);"
        + "})()</script>"
      )

    ViewLayout.layout
      Routes.recent
      activeMember.Name
      activeMember.IsAdmin
      "Recent"
      [ Elem.h1 [] [ Text.raw "Recent" ]
        Elem.div
          [ Attr.class' "recent-window-buttons" ]
          [ pill "Last 7 days" "days-7"
            pill "Last 14 days" "days-14"
            pill "Last 30 days" "days-30" ]
        Elem.details
          [ Attr.create "open" "" ]
          [ Elem.summary [ Attr.class' "chart-toggle" ] [ Text.raw "Blood Pressure Graph" ]
            Elem.div
              [ Attr.class' "chart-container" ]
              [ valueStrip
                Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ]
                scrubberScript ] ]
        section "Last 7 days" "days-7" days7
        section "Last 14 days" "days-14" days14
        section "Last 30 days" "days-30" days30 ]

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
            ViewLayout.field "Comment" "Comments" m.Comments "text"
            Elem.div
              [ Attr.class' "actions" ]
              [ Elem.button [ Attr.type' "submit" ] [ Text.raw "Save" ]
                Elem.a [ Attr.href Routes.history; Attr.role "button"; Attr.class' "secondary" ] [ Text.raw "Cancel" ] ] ] ]
