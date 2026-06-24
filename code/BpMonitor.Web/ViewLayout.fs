namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Shared layout shells and primitive helpers used by all view modules.
module ViewLayout =
  /// A single nav link, marked `aria-current="page"` (which Pico styles as active)
  /// when its href matches the page's `active` route.
  let private versionFooter () : XmlNode list =
    let v = Version.current

    match Version.releaseUrl v with
    | Some url -> [ Text.raw "BpMonitor "; Elem.a [ Attr.href url ] [ Text.raw $"v{v}" ] ]
    | None -> [ Text.raw $"BpMonitor {v}" ]

  let private navLink (active: string) (href: string) (label: string) : XmlNode =
    let attrs =
      if href = active then
        [ Attr.href href; Attr.create "aria-current" "page" ]
      else
        [ Attr.href href ]

    Elem.li [] [ Elem.a attrs [ Text.raw label ] ]

  /// The dark/light mode toggle (icon set by theme.js/theme-label.js). `extraClass`
  /// lets the login page add `theme-toggle--standalone` since it has no topbar/sidebar
  /// to host it (see app.css).
  let private themeToggleButton (extraClass: string) : XmlNode =
    Elem.button
      [ Attr.class' ($"theme-toggle {extraClass}".Trim())
        Attr.create "onclick" "toggleTheme()" ]
      []

  /// Shared <head> element. `extras` allows callers to append additional nodes
  /// (e.g., the htmx script that only the authenticated layout needs).
  let private htmlHead (title: string) (extras: XmlNode list) : XmlNode =
    Elem.head
      []
      ([ Elem.meta [ Attr.charset "utf-8" ]
         Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
         Elem.title [] [ Text.raw title ]
         Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
         // Runs once on initial load; survives hx-boost navigations because it lives in <head>.
         // No defer/async — render-blocking prevents flash of the wrong theme (FOUC).
         Elem.script [ Attr.src "/theme.js" ] []
         Elem.link [ Attr.rel "stylesheet"; Attr.href "/pico.min.css" ]
         Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ]
         // Vendored from Plotly.NET's embedded resource (see scripts/extract-plotly-js.fsx) —
         // must be blocking (no defer/async) so chart render scripts in the body can call
         // Plotly.newPlot synchronously when parsed.
         Elem.script [ Attr.src "/plotly-2.27.1.min.js"; Attr.charset "utf-8" ] [] ]
       @ extras)

  /// Page shell for authenticated pages: shared <head>, nav bar with logged-in member
  /// name + logout, and hx-boosted body.
  let layout (active: string) (memberName: string) (isAdmin: bool) (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ htmlHead title [ Elem.script [ Attr.src "/htmx.min.js" ] [] ]
        Elem.body
          [ Attr.create "hx-boost" "true" ]
          [ // Checkbox drives the mobile off-canvas drawer via pure CSS sibling selectors.
            // hx-boost re-renders <body> on every navigation, so the checkbox resets to
            // unchecked automatically — the drawer auto-closes after tapping a link.
            Elem.input
              [ Attr.type' "checkbox"
                Attr.id "nav-toggle"
                Attr.create "aria-hidden" "true" ]
            // Slim app bar: hidden on desktop, shown on mobile (and as the restore bar
            // when the desktop sidebar is collapsed) — see app.css.
            Elem.header
              [ Attr.class' "topbar" ]
              [ Elem.label
                  [ Attr.create "for" "nav-toggle"
                    Attr.class' "nav-burger"
                    Attr.create "aria-label" "Menu" ]
                  [ Text.raw "☰" ]
                Elem.span [ Attr.class' "topbar-title" ] [ Text.raw "BpMonitor" ]
                themeToggleButton "" ]
            // Second label for same checkbox: acts as the backdrop — clicking it unchecks
            // the checkbox and closes the drawer.
            Elem.label [ Attr.create "for" "nav-toggle"; Attr.class' "nav-backdrop" ] []
            Elem.nav
              [ Attr.class' "sidebar" ]
              [ Elem.ul
                  []
                  [ Elem.li
                      []
                      [ Elem.label
                          [ Attr.create "for" "nav-toggle"
                            Attr.class' "sidebar-collapse"
                            Attr.create "aria-label" "Collapse sidebar" ]
                          [ Text.raw "◀" ] ]
                    navLink active Routes.home "Home"
                    navLink active Routes.add "Add"
                    navLink active Routes.history "History"
                    navLink active Routes.recent "Recent"
                    navLink active Routes.trends "Trends"
                    navLink active Routes.settings "Settings"
                    // hx-boost="false" prevents htmx from AJAX-swapping the download response.
                    Elem.li
                      []
                      [ Elem.a
                          [ Attr.href Routes.exportJson; Attr.create "hx-boost" "false" ]
                          [ Text.raw "Export JSON" ] ]
                    // hx-boost="false" prevents htmx from AJAX-swapping the download response.
                    Elem.li
                      []
                      [ Elem.a [ Attr.href Routes.exportCsv; Attr.create "hx-boost" "false" ] [ Text.raw "Export CSV" ] ]
                    if isAdmin then
                      navLink active Routes.members "Members" ]
                // Member name and logout pinned to the bottom of the sidebar.
                Elem.div
                  [ Attr.class' "sidebar-user" ]
                  [ Elem.span [ Attr.class' "nav-member-name" ] [ Text.enc memberName ]
                    themeToggleButton ""
                    Elem.form
                      [ Attr.method "post"; Attr.action "/logout"; Attr.class' "inline" ]
                      [ Elem.button [ Attr.type' "submit"; Attr.class' "outline secondary" ] [ Text.raw "Logout" ] ] ] ]
            Elem.div
              [ Attr.class' "content" ]
              [ Elem.main [ Attr.class' "container" ] content
                Elem.footer [ Attr.class' "container" ] [ Elem.small [] (versionFooter ()) ] ]
            // Re-runs on every body render (initial + hx-boost swaps) to sync the button label.
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  /// Minimal page shell for unauthenticated pages (login). No nav, no logout.
  let loginLayout (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang "en" ]
      [ htmlHead title []
        Elem.body
          [ Attr.create "hx-boost" "false" ]
          [ themeToggleButton "theme-toggle--standalone"
            Elem.main
              [ Attr.class' "container login-container" ]
              ([ Elem.header
                   []
                   [ Elem.h1 [] [ Text.raw "BpMonitor" ]
                     Elem.p [] [ Text.raw "Blood pressure tracker" ] ] ]
               @ content)
            Elem.footer [ Attr.class' "container" ] [ Elem.small [] (versionFooter ()) ]
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  let errorBox (errors: string list) : XmlNode =
    match errors with
    | [] -> Text.raw ""
    | _ ->
      Elem.div
        [ Attr.class' "errors"; Attr.role "alert" ]
        [ Elem.ul [] (errors |> List.map (fun e -> Elem.li [] [ Text.enc e ])) ]

  /// A single labeled form field: `<div class="field"><label/><input/></div>`.
  /// Shared by readingForm, memberForm, and settingsForm.
  let field (labelText: string) (name: string) (value: string) (inputType: string) : XmlNode =
    Elem.div
      [ Attr.class' "field" ]
      [ Elem.label [ Attr.for' name ] [ Text.raw labelText ]
        Elem.input [ Attr.type' inputType; Attr.id name; Attr.name name; Attr.value value ] ]

  /// The readings' table; wrapped in an id'd container so it can be targeted for
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
