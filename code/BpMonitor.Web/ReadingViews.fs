namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for reading-related pages (landing, history, add/edit form).
module ReadingViews =
  /// A landing-page action button: an icon glyph followed by its label.
  let private actionButton (href: string) (glyph: string) (label: string) : XmlNode =
    Elem.a
      [ Attr.href href; Attr.role "button" ]
      [ Elem.span [ Attr.class' "icon" ] [ Text.raw glyph ]; Text.raw label ]

  /// Same as `actionButton`, but opts the link out of hx-boost so file-download
  /// responses (exports) aren't AJAX-swapped into the page.
  let private downloadActionButton (href: string) (glyph: string) (label: string) : XmlNode =
    Elem.a
      [ Attr.href href; Attr.role "button"; Attr.create "hx-boost" "false" ]
      [ Elem.span [ Attr.class' "icon" ] [ Text.raw glyph ]; Text.raw label ]

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
          [ Attr.class' "home-actions" ]
          [ actionButton Routes.add "➕" "Add reading"
            actionButton Routes.history "📜" "History"
            actionButton Routes.trends "📈" "Trends"
            actionButton Routes.recent "🕒" "Recent"
            actionButton Routes.settings "⚙️" "Settings"
            downloadActionButton Routes.exportJson "⬇️" "Export JSON"
            downloadActionButton Routes.exportCsv "⬇️" "Export CSV"
            if m.IsAdmin then
              actionButton Routes.members "👥" "Members" ] ]

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

  /// Recent: chart of all readings, focused on the last 30 days, with a sys/dias value strip.
  let recent
    (activeMember: FamilyMember)
    (chartHtml: string)
    (allReadings: BloodPressureReading list)
    (cutoff: System.DateTimeOffset)
    : XmlNode =
    let valueStrip =
      // The strip lists every loaded reading (chart now loads all of them so panning
      // reveals older data); cells older than `cutoff` start hidden via `out-of-range`
      // (see scrubberScript below, which un-hides them in sync as the user pans).
      let chronological = allReadings |> List.sortBy _.Timestamp

      // Each cell is tagged with the same x-label the chart uses for this reading
      // (Charts.fs `seriesOf` formats x as Formats.formatLocal r.Timestamp), so the
      // scrubber script below can match a hovered chart point back to its strip column.
      //
      // Fig. 5's data table color-codes each value by where it falls relative to the
      // member's goal range (Wegier et al. 2021): out-of-range values are highlighted,
      // in-range values stay neutral. See app.css `.value-strip-value.above/.below`.
      let cellClass (position: RangePosition) =
        match position with
        | Above -> "value-strip-value above"
        | Below -> "value-strip-value below"
        | InRange -> "value-strip-value"

      let row (label: string) (value: BloodPressureReading -> int) (classify: int -> RangePosition) =
        Elem.tr
          []
          [ yield Elem.th [ Attr.scope "row"; Attr.class' "value-strip-label" ] [ Text.raw label ]
            for r in chronological ->
              let v = value r

              // Cells outside the 30-day focus window start hidden (`out-of-range`, same
              // class the relayout listener below toggles on pan/zoom), so the initial
              // view matches the chart's initial x-axis range even though all readings
              // are loaded.
              let staleClass = if r.Timestamp < cutoff then " out-of-range" else ""

              Elem.td
                [ Attr.class' (cellClass (classify v) + staleClass)
                  Attr.create "data-x" (Formats.formatLocal r.Timestamp) ]
                [ Text.raw (string v) ] ]

      Elem.div
        [ Attr.class' "value-strip" ]
        [ Elem.table
            []
            [ Elem.tbody
                []
                [ row "Systolic" _.Systolic (GoalRange.classifySystolic activeMember.Goal)
                  row "Diastolic" _.Diastolic (GoalRange.classifyDiastolic activeMember.Goal) ] ] ]

    // Fig. 5's scrubber bar (Wegier et al. 2021): the chart's x-axis spike (Charts.fs
    // `recentXAxis`) already draws the moving vertical line; this links it to the value
    // strip by boxing the hovered column. Follows the same poll-until-ready pattern as
    // the chart's own errorBarScript (Charts.fs `errorBarScript`).
    // TODOs.md: keep the value strip in sync with the chart's x-axis when zooming/panning.
    // `plotly_relayout` fires with `xaxis.range[0]`/`[1]` on zoom/pan, or `xaxis.autorange`
    // on a reset (e.g. double-click). Columns whose data-x falls outside the new range get
    // `out-of-range` (display:none in app.css), so the fixed-layout table redistributes the
    // remaining columns to stay aligned with what the chart is showing.
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
        + "d.on('plotly_relayout',function(e){"
        + "var cells=document.querySelectorAll('.value-strip td[data-x]');"
        + "var lo=e['xaxis.range[0]'],hi=e['xaxis.range[1]'];"
        + "if(lo===undefined&&Array.isArray(e['xaxis.range'])){lo=e['xaxis.range'][0];hi=e['xaxis.range'][1];}"
        + "if(lo===undefined&&e['xaxis.autorange']===undefined&&d.layout&&d.layout.xaxis){"
        + "if(d.layout.xaxis.autorange){lo=undefined;}"
        + "else if(d.layout.xaxis.range){lo=d.layout.xaxis.range[0];hi=d.layout.xaxis.range[1];}"
        + "}"
        + "if(lo===undefined||hi===undefined){"
        + "cells.forEach(function(c){c.classList.remove('out-of-range');});"
        + "return;"
        + "}"
        + "var loT=new Date(String(lo).replace(' ','T')).getTime();"
        + "var hiT=new Date(String(hi).replace(' ','T')).getTime();"
        + "if(isNaN(loT)||isNaN(hiT))return;"
        + "cells.forEach(function(c){"
        + "var t=new Date(c.dataset.x.replace(' ','T')).getTime();"
        + "if(isNaN(t))return;"
        + "c.classList.toggle('out-of-range',t<loT||t>hiT);"
        + "});"
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
          [ Attr.class' "chart-container" ]
          [ valueStrip
            Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ]
            scrubberScript ] ]

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
