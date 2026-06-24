namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for the Trends pages.
module TrendViews =
  /// The swappable panel: granularity toggle + sub-period strip + stats + inline chart.
  /// Rendered as a fragment for htmx swaps (GET /trends/{gran} and GET /trends/{gran}/{key});
  /// also used directly by the full /trends page, so the buttons are always inside the swapped region.
  let trendsPanel
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
      let s = TrendPeriod.slug g

      let label =
        match g with
        | Weekly -> "Weekly"
        | Monthly -> "Monthly"
        | Yearly -> "Yearly"

      let baseAttrs =
        [ Attr.href $"/trends/{s}"
          Attr.role "button"
          Attr.create "hx-get" $"/trends/{s}"
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

      if not hasData && not isActive then
        Elem.a
          [ Attr.role "button"
            Attr.class' "outline"
            Attr.create "aria-disabled" "true" ]
          [ Text.raw p.Label ]
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

        Elem.a attrs [ Text.raw p.Label ]

    // ── Content ──────────────────────────────────────────────────────────────
    let content =
      if summary.Count = 0 then
        [ Elem.p [ Attr.class' "trends-empty" ] [ Text.enc $"No readings in {summary.Label}." ] ]
      else
        let simpleRow (label: string) (value: string) =
          Elem.tr
            []
            [ Elem.th [ Attr.scope "row" ] [ Text.raw label ]
              Elem.td [] [ Text.raw value ] ]

        let statRow (label: string) (unit: string) (avg: int) (mn: int) (mx: int) =
          simpleRow $"{label} ({unit})" $"{avg} (min: {mn}, max: {mx})"

        [ Elem.table
            [ Attr.class' "trends-stats" ]
            [ Elem.tbody
                []
                [ simpleRow "Readings" (string summary.Count)
                  statRow "Avg Systolic" "mmHg" summary.AvgSystolic summary.MinSystolic summary.MaxSystolic
                  statRow "Avg Diastolic" "mmHg" summary.AvgDiastolic summary.MinDiastolic summary.MaxDiastolic
                  statRow "Avg Heart Rate" "bpm" summary.AvgHeartRate summary.MinHeartRate summary.MaxHeartRate ] ]
          Elem.div [ Attr.class' "chart" ] [ Text.raw chartHtml ]
          ViewLayout.readingsTable readings ]

    Elem.div
      [ Attr.id "trends-panel" ]
      [ Elem.div [ Attr.class' "trends-window-buttons" ] ([ Weekly; Monthly; Yearly ] |> List.map granButton)
        // Scroller wrapper hosts the edge-fade overlays (CSS) and is read by
        // wwwroot/trends-scroll.js to toggle them and to center the active pill.
        Elem.div
          [ Attr.class' "trends-subperiod-scroller" ]
          [ Elem.div [ Attr.class' "trends-subperiod-buttons" ] (periods |> List.map periodButton) ]
        yield! content ]

  /// The /trends full page. Pre-renders the Weekly/current panel (including toggle buttons).
  let trends
    (m: FamilyMember)
    (summary: WindowSummary)
    (periods: TrendPeriod list)
    (periodsWithData: Set<string>)
    (readings: BloodPressureReading list)
    (chartHtml: string)
    : XmlNode =
    ViewLayout.layout
      "/trends"
      m.Name
      m.IsAdmin
      "Trends"
      [ Elem.h1 [] [ Text.raw "Trends" ]
        trendsPanel summary periods periodsWithData readings chartHtml ]
