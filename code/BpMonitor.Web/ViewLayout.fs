namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Shared layout shells and primitive helpers used by all view modules.
module ViewLayout =
  /// A single nav link, marked `aria-current="page"` (which Pico styles as active)
  /// when its href matches the page's `active` route.
  let private navLink (active: string) (href: string) (label: string) : XmlNode =
    let attrs =
      if href = active then
        [ Attr.href href; Attr.create "aria-current" "page" ]
      else
        [ Attr.href href ]

    Elem.li [] [ Elem.a attrs [ Text.raw label ] ]

  /// Page shell for authenticated pages: shared <head>, nav bar with logged-in member
  /// name + logout, and hx-boosted body.
  let layout (active: string) (memberName: string) (isAdmin: bool) (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ Elem.head
          []
          [ Elem.meta [ Attr.charset "utf-8" ]
            Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
            // Runs once on initial load; survives hx-boost navigations because it lives in <head>.
            // No defer/async — render-blocking prevents flash of wrong theme (FOUC).
            Elem.script [ Attr.src "/theme.js" ] []
            Elem.link
              [ Attr.rel "stylesheet"
                Attr.href "https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css" ]
            Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ]
            Elem.script [ Attr.src "/htmx.min.js" ] [] ]
        Elem.body
          [ Attr.create "hx-boost" "true" ]
          [ Elem.nav
              [ Attr.class' "container" ]
              [ Elem.ul
                  []
                  [ Elem.li [] [ Elem.strong [] [ Text.raw "BpMonitor" ] ]
                    navLink active "/" "Home"
                    navLink active "/add" "Add"
                    navLink active Routes.history "History"
                    navLink active "/trends" "Trends"
                    if isAdmin then
                      navLink active Routes.members "Members" ]
                Elem.ul
                  []
                  [ Elem.li [] [ Elem.span [ Attr.class' "nav-member-name" ] [ Text.enc memberName ] ]
                    Elem.li
                      []
                      [ Elem.form
                          [ Attr.method "post"; Attr.action "/logout"; Attr.class' "inline" ]
                          [ Elem.button [ Attr.type' "submit"; Attr.class' "outline secondary" ] [ Text.raw "Logout" ] ] ]
                    Elem.li
                      []
                      [ Elem.button
                          [ Attr.id "theme-toggle"
                            Attr.class' "outline secondary"
                            Attr.create "onclick" "toggleTheme()" ]
                          [] ] ] ]
            Elem.main [ Attr.class' "container" ] content
            Elem.footer
              [ Attr.class' "container" ]
              [ Elem.small
                  []
                  (let v = Version.current

                   match Version.releaseUrl v with
                   | Some url -> [ Text.raw "BpMonitor "; Elem.a [ Attr.href url ] [ Text.raw $"v{v}" ] ]
                   | None -> [ Text.raw $"BpMonitor {v}" ]) ]
            // Re-runs on every body render (initial + hx-boost swaps) to sync the button label.
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  /// Minimal page shell for unauthenticated pages (login). No nav, no logout.
  let loginLayout (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ Elem.head
          []
          [ Elem.meta [ Attr.charset "utf-8" ]
            Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
            Elem.script [ Attr.src "/theme.js" ] []
            Elem.link
              [ Attr.rel "stylesheet"
                Attr.href "https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css" ]
            Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ] ]
        Elem.body
          [ Attr.create "hx-boost" "false" ]
          [ Elem.nav
              [ Attr.class' "container" ]
              [ Elem.ul [] []
                Elem.ul
                  []
                  [ Elem.li
                      []
                      [ Elem.button
                          [ Attr.id "theme-toggle"
                            Attr.class' "outline secondary"
                            Attr.create "onclick" "toggleTheme()" ]
                          [] ] ] ]
            Elem.main
              [ Attr.class' "container login-container" ]
              ([ Elem.header
                   []
                   [ Elem.h1 [] [ Text.raw "BpMonitor" ]
                     Elem.p [] [ Text.raw "Blood pressure tracker" ] ] ]
               @ content)
            Elem.footer
              [ Attr.class' "container" ]
              [ Elem.small
                  []
                  (let v = Version.current

                   match Version.releaseUrl v with
                   | Some url -> [ Text.raw "BpMonitor "; Elem.a [ Attr.href url ] [ Text.raw $"v{v}" ] ]
                   | None -> [ Text.raw $"BpMonitor {v}" ]) ]
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  let errorBox (errors: string list) : XmlNode =
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
