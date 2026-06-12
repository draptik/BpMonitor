namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for the Trends pages.
module TrendViews =
  /// The swappable panel: granularity toggle + sub-period strip + stats + chart iframe.
  /// Rendered as a fragment for htmx swaps (GET /trends/{gran} and GET /trends/{gran}/{key});
  /// also used directly by the full /trends page so the buttons are always inside the swapped region.
  let trendsPanel (summary: WindowSummary) (periods: TrendPeriod list) (readings: BloodPressureReading list) : XmlNode =
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
      let href = $"/trends/{granSlug}/{p.Key}"

      let baseAttrs =
        [ Attr.href href
          Attr.role "button"
          Attr.create "hx-get" href
          Attr.create "hx-target" "#trends-panel"
          Attr.create "hx-swap" "outerHTML" ]

      let attrs =
        if p.Key = summary.PeriodKey then
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
          Elem.iframe
            [ Attr.create "data-chart-src" $"/chart?gran={granSlug}&period={summary.PeriodKey}"
              Attr.class' "chart"
              Attr.title $"Blood Pressure — {summary.Label}" ]
            []
          ViewLayout.readingsTable readings ]

    Elem.div
      [ Attr.id "trends-panel" ]
      [ Elem.div [ Attr.class' "trends-window-buttons" ] ([ Weekly; Monthly; Yearly ] |> List.map granButton)
        Elem.div [ Attr.class' "trends-subperiod-buttons" ] (periods |> List.map periodButton)
        yield! content
        // Scroll the active sub-period pill into view (runs after htmx swaps in this fragment).
        Elem.script
          []
          [ Text.raw
              "(function(){var a=document.querySelector('.trends-subperiod-buttons [aria-current=\"page\"]');if(a)a.scrollIntoView({inline:'nearest',block:'nearest'});})()" ] ]

  /// The /trends full page. Pre-renders the Weekly/current panel (including toggle buttons).
  let trends
    (m: FamilyMember)
    (summary: WindowSummary)
    (periods: TrendPeriod list)
    (readings: BloodPressureReading list)
    : XmlNode =
    ViewLayout.layout
      "/trends"
      m.Name
      m.IsAdmin
      "Trends"
      [ Elem.h1 [] [ Text.raw "Trends" ]; trendsPanel summary periods readings ]
