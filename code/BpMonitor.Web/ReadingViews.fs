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
  let landing (s: Strings) (m: FamilyMember) : XmlNode =
    ViewLayout.layout
      s
      Routes.home
      m.Name
      m.IsAdmin
      s.Reading.LandingTitle
      [ Elem.h1 [] [ Text.raw s.Reading.LandingTitle ]
        Elem.p [] [ Text.raw s.Reading.LandingTagline ]
        Elem.div
          [ Attr.class' "home-actions" ]
          [ actionButton Routes.add "➕" s.Reading.AddReadingTitle
            actionButton Routes.recent "🕒" s.Shell.NavRecent
            actionButton Routes.trends "📈" s.Shell.NavTrends
            actionButton Routes.history "📜" s.Shell.NavHistory ]
        Elem.div
          [ Attr.class' "home-actions home-actions-secondary" ]
          [ downloadActionButton Routes.exportJson "⬇️" s.Shell.NavExportJson
            downloadActionButton Routes.exportCsv "⬇️" s.Shell.NavExportCsv
            actionButton Routes.settings "⚙️" s.Shell.NavSettings
            if m.IsAdmin then
              actionButton Routes.members "👥" s.Shell.NavMembers ] ]

  /// History: chart, then the Medications Timeline — order matters for plot-ready.js.
  let history
    (s: Strings)
    (activeMember: FamilyMember)
    (chartHtml: string)
    (readings: BloodPressureReading list)
    (medicationsPanel: XmlNode)
    : XmlNode =
    ViewLayout.layout
      s
      Routes.history
      activeMember.Name
      activeMember.IsAdmin
      s.Reading.HistoryTitle
      [ Elem.h1 [] [ Text.raw s.Reading.HistoryTitle ]
        Elem.details
          []
          [ Elem.summary [ Attr.class' "chart-toggle" ] [ Text.raw s.Reading.BloodPressureGraph ]
            Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ] ]
        medicationsPanel
        ViewLayout.readingsTable s readings ]

  /// The swappable chart container: zoom/load-full buttons, value strip, chart, citation.
  let recentChartContainer
    (s: Strings)
    (activeMember: FamilyMember)
    (chartHtml: string)
    (allReadings: BloodPressureReading list)
    (windowStart: System.DateTimeOffset)
    (now: System.DateTimeOffset)
    (zoomShortcutDays: (string * float) list)
    (showLoadFull: bool)
    (medicationsPanel: XmlNode)
    : XmlNode =
    let valueStrip =
      // Lists every loaded reading; cells older than `windowStart` start hidden via `out-of-range`.
      let chronological = allReadings |> List.sortBy _.Timestamp

      // Fig. 5 color-codes each value by goal-range position (Wegier et al. 2021); see app.css.
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
              // Cells outside the 30-day focus window start hidden; pan/zoom toggles this.
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
                [ row s.Table.Systolic _.Systolic (GoalRange.classifySystolic activeMember.Goal)
                  row s.Table.Diastolic _.Diastolic (GoalRange.classifyDiastolic activeMember.Goal) ] ] ]

    // Snaps the chart's x-axis via Plotly.relayout (wwwroot/recent-zoom.js).
    let hiFormatted = Formats.formatLocal now

    // The button matching `windowStart` renders as the active pill on load.
    let zoomButton (label: string) (days: float) =
      let lo = now.AddDays(-days)

      Elem.button
        [ Attr.type' "button"
          Attr.class' "recent-zoom-button outline"
          Attr.create "aria-pressed" (if lo = windowStart then "true" else "false")
          Attr.create "data-lo" (Formats.formatLocal lo)
          Attr.create "data-hi" hiFormatted ]
        [ Text.raw label ]

    let zoomButtons =
      Elem.div [ Attr.class' "recent-zoom-buttons" ] [ for label, days in zoomShortcutDays -> zoomButton label days ]

    // Shown while older readings are hidden; htmx-swaps in the full history (GET /recent/full).
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
            [ Text.raw s.Reading.LoadFullHistory ] ]

    // Fig. 5's scrubber bar: boxes the hovered column in sync with the chart's x-axis spike.
    Elem.div
      [ Attr.id "recent-chart"; Attr.class' "chart-container" ]
      ([ zoomButtons ]
       @ loadFullButton
       @ [ valueStrip
           Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ]
           medicationsPanel
           Elem.p
             [ Attr.class' "chart-citation" ]
             [ Text.raw s.Reading.ChartCitationPrefix
               Elem.a [ Attr.href "https://doi.org/10.1186/s12911-021-01598-4" ] [ Text.raw "Wegier et al. 2021" ] ] ])

  /// Recent: chart of all readings, focused on the last 30 days, with a sys/dias value strip.
  let recent
    (s: Strings)
    (activeMember: FamilyMember)
    (chartHtml: string)
    (allReadings: BloodPressureReading list)
    (windowStart: System.DateTimeOffset)
    (now: System.DateTimeOffset)
    (zoomShortcutDays: (string * float) list)
    (showLoadFull: bool)
    (medicationsPanel: XmlNode)
    : XmlNode =
    ViewLayout.layout
      s
      Routes.recent
      activeMember.Name
      activeMember.IsAdmin
      s.Reading.RecentTitle
      [ Elem.h1 [] [ Text.raw s.Reading.RecentTitle ]
        recentChartContainer
          s
          activeMember
          chartHtml
          allReadings
          windowStart
          now
          zoomShortcutDays
          showLoadFull
          medicationsPanel ]

  /// Shared add/edit form. `action` is the POST target; `errors` are rendered
  /// above the fields when re-displaying after a failed submit.
  let readingForm
    (s: Strings)
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
      s
      active
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ fieldWithHint s.Table.Timestamp s.Reading.TimestampHint FormFields.timestamp m.Timestamp "text"
            fieldWithHint s.Table.Systolic s.Table.MmHg FormFields.systolic m.Systolic "number"
            fieldWithHint s.Table.Diastolic s.Table.MmHg FormFields.diastolic m.Diastolic "number"
            fieldWithHint s.Table.HeartRate s.Table.Bpm FormFields.heartRate m.HeartRate "number"
            ViewLayout.field s.Shell.Comment FormFields.comments m.Comments "text"
            ViewLayout.formActions s Routes.history ] ]
