namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for the Trends pages.
module TrendViews =
  /// Renders a `PeriodLabel` DU value through `LocalizedStrings.Trend`.
  let renderPeriodLabel (s: LocalizedStrings) (label: PeriodLabel) : string =
    match label with
    | ThisWeek -> s.Trend.ThisWeek
    | LastWeek -> s.Trend.LastWeek
    | CalendarWeek week -> s.Trend.CalendarWeek week
    | CalendarWeekOfYear(week, year) -> s.Trend.CalendarWeekOfYear week year
    | ThisMonth -> s.Trend.ThisMonth
    | LastMonth -> s.Trend.LastMonth
    | MonthOfYear(month, year) -> s.Trend.MonthOfYear month year
    | ThisYear -> s.Trend.ThisYear
    | LastYear -> s.Trend.LastYear
    | Year year -> s.Trend.Year year

  /// The swappable panel: granularity toggle + sub-period strip + stats + inline chart.
  /// Rendered as a fragment for htmx swaps and used directly by the full /trends page.
  let trendsPanel
    (s: LocalizedStrings)
    (summary: WindowSummary)
    (periods: TrendPeriod list)
    (periodsWithData: Set<string>)
    (readings: BloodPressureReading list)
    (chartHtml: string)
    : XmlNode =
    let gran = summary.Granularity
    let granSlug = TrendPeriod.slug gran

    // ── Level 1: granularity pills ───────────────────────────────────────────
    let granButton (g: Granularity) =
      let slug = TrendPeriod.slug g

      let label =
        match g with
        | Weekly -> s.Trend.Weekly
        | Monthly -> s.Trend.Monthly
        | Yearly -> s.Trend.Yearly

      let baseAttrs =
        [ Attr.href $"/trends/{slug}"
          Attr.role "button"
          Attr.create "hx-get" $"/trends/{slug}"
          Attr.create "hx-target" "#trends-panel"
          Attr.create "hx-swap" "outerHTML" ]

      let attrs =
        if g = gran then
          baseAttrs @ [ Attr.create "aria-current" "page" ]
        else
          baseAttrs @ [ Attr.class' "outline" ]

      Elem.a attrs [ Text.raw label ]

    // ── Level 2: sub-period pills ────────────────────────────────────────────
    let periodButton (p: TrendPeriod) =
      let isActive = p.Key = summary.PeriodKey
      let hasData = periodsWithData |> Set.contains p.Key
      let label = renderPeriodLabel s p.Label

      if not hasData && not isActive then
        Elem.a
          [ Attr.role "button"
            Attr.class' "outline"
            Attr.create "aria-disabled" "true" ]
          [ Text.raw label ]
      else
        let href = $"/trends/{granSlug}/{p.Key}"

        let baseAttrs =
          [ Attr.href href
            Attr.role "button"
            Attr.create "hx-get" href
            Attr.create "hx-target" "#trends-panel"
            Attr.create "hx-swap" "outerHTML" ]

        let attrs =
          if isActive then
            baseAttrs @ [ Attr.create "aria-current" "page" ]
          else
            baseAttrs @ [ Attr.class' "outline" ]

        Elem.a attrs [ Text.raw label ]

    // ── Content ──────────────────────────────────────────────────────────────
    let content =
      if summary.Count = 0 then
        [ Elem.p [ Attr.class' "trends-empty" ] [ Text.enc (s.Trend.NoReadingsIn(renderPeriodLabel s summary.Label)) ] ]
      else
        let simpleRow (label: string) (value: string) =
          Elem.tr
            []
            [ Elem.th [ Attr.scope "row" ] [ Text.raw label ]
              Elem.td [] [ Text.raw value ] ]

        let statRow (label: string) (unit: string) (avg: int) (mn: int) (mx: int) =
          simpleRow $"{label} ({unit})" (s.Trend.StatValue avg mn mx)

        [ Elem.table
            [ Attr.class' "trends-stats" ]
            [ Elem.tbody
                []
                [ simpleRow s.Trend.Readings (string summary.Count)
                  statRow s.Trend.AvgSystolic s.Table.MmHg summary.AvgSystolic summary.MinSystolic summary.MaxSystolic
                  statRow
                    s.Trend.AvgDiastolic
                    s.Table.MmHg
                    summary.AvgDiastolic
                    summary.MinDiastolic
                    summary.MaxDiastolic
                  statRow
                    s.Trend.AvgHeartRate
                    s.Table.Bpm
                    summary.AvgHeartRate
                    summary.MinHeartRate
                    summary.MaxHeartRate ] ]
          Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ]
          ViewLayout.readingsTable s readings ]

    Elem.div
      [ Attr.id "trends-panel" ]
      [ Elem.div [ Attr.class' "trends-window-buttons" ] ([ Weekly; Monthly; Yearly ] |> List.map granButton)
        // Scroller wrapper hosts the edge-fade overlays; wwwroot/trends-scroll.js toggles them.
        Elem.div
          [ Attr.class' "trends-subperiod-scroller" ]
          [ Elem.div [ Attr.class' "trends-subperiod-buttons" ] (periods |> List.map periodButton) ]
        yield! content ]

  /// The /trends full page. Pre-renders the Weekly/current panel (including toggle buttons).
  let trends
    (s: LocalizedStrings)
    (m: FamilyMember)
    (summary: WindowSummary)
    (periods: TrendPeriod list)
    (periodsWithData: Set<string>)
    (readings: BloodPressureReading list)
    (chartHtml: string)
    : XmlNode =
    ViewLayout.layout
      s
      Routes.trends
      m.Name
      m.IsAdmin
      s.Trend.TrendsTitle
      [ Elem.h1 [] [ Text.raw s.Trend.TrendsTitle ]
        trendsPanel s summary periods periodsWithData readings chartHtml ]
