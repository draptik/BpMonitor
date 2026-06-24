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

  /// Same as `actionButton`, but opts the link out of hx-boost, so file-download
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

  /// History: chart above the readings' table.
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

  /// The swappable chart container: zoom/load-full buttons, value strip, chart, citation.
  /// Rendered as a fragment for htmx swaps (GET /recent/full); also used directly by the
  /// full /recent page, so the buttons are always inside the swapped region.
  let recentChartContainer
    (activeMember: FamilyMember)
    (chartHtml: string)
    (allReadings: BloodPressureReading list)
    (windowStart: System.DateTimeOffset)
    (now: System.DateTimeOffset)
    (zoomShortcutDays: (string * float) list)
    (showLoadFull: bool)
    : XmlNode =
    let valueStrip =
      // The strip lists every loaded reading (the chart's load window, see ReadingHandlers
      // — bounded but wider than the visible focus); cells older than `windowStart` start
      // hidden via `out-of-range` (see scrubberScript below, which un-hides them in sync as
      // the user pans).
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
              let staleClass = if r.Timestamp < windowStart then " out-of-range" else ""

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

    // Shortcut buttons that snap the chart's x-axis to a fixed window via
    // Plotly.relayout (wwwroot/recent-zoom.js). Each button's range is rendered
    // server-side from the same `now` the handler passed to the chart, so the format
    // (Formats.formatLocal) and anchor always match exactly. The existing
    // recent-scrubber.js `plotly_relayout` listener re-syncs the value strip's
    // out-of-range cells automatically when relayout fires.
    let hiFormatted = Formats.formatLocal now

    let zoomButton (label: string) (days: float) =
      Elem.button
        [ Attr.type' "button"
          Attr.class' "recent-zoom-button"
          Attr.create "data-lo" (Formats.formatLocal (now.AddDays(-days)))
          Attr.create "data-hi" hiFormatted ]
        [ Text.raw label ]

    let zoomButtons =
      Elem.div [ Attr.class' "recent-zoom-buttons" ] [ for label, days in zoomShortcutDays -> zoomButton label days ]

    // Shown only while the load window (ReadingHandlers `recentLoadWindowDays`) is
    // hiding older readings; clicking it htmx-swaps this whole container for one
    // rendered from the member's entire history (GET /recent/full), same pattern as
    // /trends' panel swap.
    let loadFullButton =
      if not showLoadFull then
        []
      else
        [ Elem.button
            [ Attr.type' "button"
              Attr.class' "recent-load-full"
              Attr.create "hx-get" Routes.recentFull
              Attr.create "hx-target" "#recent-chart"
              Attr.create "hx-swap" "outerHTML" ]
            [ Text.raw "Load full history" ] ]

    // Fig. 5's scrubber bar (Wegier et al. 2021): the chart's x-axis spike (Charts.fs
    // `recentXAxis`) already draws the moving vertical line; this links it to the value
    // strip by boxing the hovered column. Behavior lives in wwwroot/recent-scrubber.js
    // (loaded globally by ViewLayout, self-guards on `.value-strip`'s presence).
    Elem.div
      [ Attr.id "recent-chart"; Attr.class' "chart-container" ]
      ([ zoomButtons ]
       @ loadFullButton
       @ [ valueStrip
           Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ]
           Elem.p
             [ Attr.class' "chart-citation" ]
             [ Text.raw "Chart layout inspired by "
               Elem.a [ Attr.href "https://doi.org/10.1186/s12911-021-01598-4" ] [ Text.raw "Wegier et al. 2021" ] ] ])

  /// Recent: chart of all readings, focused on the last 30 days, with a sys/dias value strip.
  let recent
    (activeMember: FamilyMember)
    (chartHtml: string)
    (allReadings: BloodPressureReading list)
    (windowStart: System.DateTimeOffset)
    (now: System.DateTimeOffset)
    (zoomShortcutDays: (string * float) list)
    (showLoadFull: bool)
    : XmlNode =
    ViewLayout.layout
      Routes.recent
      activeMember.Name
      activeMember.IsAdmin
      "Recent"
      [ Elem.h1 [] [ Text.raw "Recent" ]
        recentChartContainer activeMember chartHtml allReadings windowStart now zoomShortcutDays showLoadFull ]

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
