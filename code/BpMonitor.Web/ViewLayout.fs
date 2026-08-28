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

  let private navIcon (glyph: string) (label: string) : XmlNode list =
    [ Elem.span [ Attr.class' "icon" ] [ Text.raw glyph ]; Text.raw label ]

  let private navLink (active: string) (href: string) (glyph: string) (label: string) : XmlNode =
    let aAttrs =
      [ yield Attr.href href
        if href = active then
          yield Attr.create "aria-current" "page" ]

    Elem.li [] [ Elem.a aAttrs (navIcon glyph label) ]

  let private navActionLink (active: string) (href: string) (glyph: string) (label: string) : XmlNode =
    let aAttrs =
      [ yield Attr.href href
        yield Attr.class' "nav-action"
        yield Attr.role "button"
        if href = active then
          yield Attr.create "aria-current" "page" ]

    Elem.li [ Attr.class' "nav-action-item" ] [ Elem.a aAttrs (navIcon glyph label) ]

  /// Opts the link out of hx-boost so file-download responses aren't AJAX-swapped into the page.
  let private navDownloadLink (href: string) (glyph: string) (label: string) : XmlNode =
    Elem.li [] [ Elem.a [ Attr.href href; Attr.create "hx-boost" "false" ] (navIcon glyph label) ]

  /// The dark/light mode toggle; `extraClass` lets the login page style itself standalone.
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
         Elem.title [] [ Text.enc title ]
         Elem.link [ Attr.rel "icon"; Attr.href "/favicon.svg"; Attr.type' "image/svg+xml" ]
         // Runs once on initial load; survives hx-boost navigations because it lives in <head>.
         // No defer/async — render-blocking prevents flash of the wrong theme (FOUC).
         Elem.script [ Attr.src "/theme.js" ] []
         // Behavior-only (no FOUC concern); each self-guards on the page elements it
         // needs and re-runs on htmx:afterSettle to survive hx-boost swaps. Deferred so
         // they don't block HTML parsing — they only register DOMContentLoaded/htmx
         // listeners, and defer preserves document order, so plot-ready.js still
         // defines the whenPlotReady helper before the chart scripts below run.
         Elem.script [ Attr.src "/plot-ready.js"; Attr.create "defer" "" ] []
         Elem.script [ Attr.src "/chart-hover.js"; Attr.create "defer" "" ] []
         Elem.script [ Attr.src "/recent-scrubber.js"; Attr.create "defer" "" ] []
         Elem.script [ Attr.src "/recent-zoom.js"; Attr.create "defer" "" ] []
         Elem.script [ Attr.src "/medications-sync.js"; Attr.create "defer" "" ] []
         Elem.script [ Attr.src "/details-memory.js"; Attr.create "defer" "" ] []
         Elem.script [ Attr.src "/trends-scroll.js"; Attr.create "defer" "" ] []
         Elem.link [ Attr.rel "stylesheet"; Attr.href "/pico.min.css" ]
         Elem.link [ Attr.rel "stylesheet"; Attr.href "/app.css" ]
         // Vendored from Plotly.NET's embedded resource (see scripts/extract-plotly-js.fsx) —
         // must be blocking (no defer/async) so chart render scripts in the body can call
         // Plotly.newPlot synchronously when parsed.
         Elem.script [ Attr.src "/plotly-2.27.1.min.js"; Attr.charset "utf-8" ] [] ]
       @ extras)

  /// Inline POST form containing a single secondary outline submit button — used
  /// wherever a destructive or secondary action needs no surrounding form.
  let inlinePostButton (action: string) (label: string) : XmlNode =
    Elem.form
      [ Attr.method "post"; Attr.action action; Attr.class' "inline" ]
      [ Elem.button [ Attr.type' "submit"; Attr.class' "outline secondary" ] [ Text.raw label ] ]

  /// Like `inlinePostButton`, styled as destructive and gated by an `hx-confirm` prompt.
  /// `hx-confirm` must sit on the form: htmx's boost resolves a submit to the form as the triggering element and only walks up from there.
  let inlineDangerPostButton (action: string) (label: string) (confirmMessage: string) : XmlNode =
    Elem.form
      [ Attr.method "post"
        Attr.action action
        Attr.class' "inline"
        Attr.create "hx-confirm" confirmMessage ]
      [ Elem.button [ Attr.type' "submit"; Attr.class' "outline button-danger" ] [ Text.raw label ] ]

  /// Page shell for authenticated pages: shared <head>, nav bar with logged-in member
  /// name + logout, and hx-boosted body.
  let layout
    (s: LocalizedStrings)
    (active: string)
    (memberName: string)
    (isAdmin: bool)
    (title: string)
    (content: XmlNode list)
    : XmlNode =
    Elem.html
      [ Attr.lang (Language.code s.Language) ]
      [ htmlHead
          title
          [ // htmx merges this into htmx.config on init (declarative equivalent of an
            // inline `htmx.config.responseHandling = ...` script, with no
            // script-ordering dependency): swap 422 validation re-renders without
            // treating them as errors; never swap other 4xx/5xx. Falco.Markup renders
            // attribute values verbatim, so the JSON's quotes must be entity-escaped
            // by hand (the browser decodes them back before htmx JSON-parses).
            Elem.meta
              [ Attr.name "htmx-config"
                Attr.content (
                  """{"responseHandling":[{"code":"204","swap":false},{"code":"[23]..","swap":true},{"code":"422","swap":true,"error":false},{"code":"[45]..","swap":false,"error":true}]}"""
                    .Replace("\"", "&quot;")
                ) ]
            Elem.script [ Attr.src "/htmx.min.js" ] [] ]
        Elem.body
          [ Attr.create "hx-boost" "true" ]
          [ // Checkbox drives the mobile off-canvas drawer via pure CSS sibling selectors.
            // hx-boost re-renders <body> on every navigation, so the checkbox resets to
            // unchecked automatically — the drawer auto-closes after tapping a link.
            Elem.input
              [ Attr.type' "checkbox"
                Attr.id "nav-toggle"
                Attr.create "aria-hidden" "true" ]
            // Slim app bar: always visible — anchors the ☰ collapse/expand toggle and
            // the theme toggle across all screen sizes — see app.css.
            Elem.header
              [ Attr.class' "topbar" ]
              [ Elem.label
                  [ Attr.create "for" "nav-toggle"
                    Attr.class' "nav-burger"
                    Attr.create "aria-label" s.Shell.Menu ]
                  [ Text.raw "☰" ]
                Elem.a [ Attr.class' "topbar-title"; Attr.href Routes.home ] [ Text.raw "BpMonitor" ]
                Elem.div
                  [ Attr.class' "topbar-right" ]
                  [ Elem.span [ Attr.class' "nav-member-name" ] [ Text.enc memberName ]
                    themeToggleButton ""
                    inlinePostButton Routes.logout s.Shell.Logout ] ]
            // Second label for same checkbox: acts as the backdrop — clicking it unchecks
            // the checkbox and closes the drawer.
            Elem.label [ Attr.create "for" "nav-toggle"; Attr.class' "nav-backdrop" ] []
            Elem.nav
              [ Attr.class' "sidebar" ]
              [ Elem.ul
                  []
                  [ navActionLink active Routes.add "➕" s.Shell.NavAdd
                    navLink active Routes.recent "🕒" s.Shell.NavRecent
                    navLink active Routes.trends "📈" s.Shell.NavTrends
                    navLink active Routes.history "📜" s.Shell.NavHistory ]
                Elem.ul
                  [ Attr.class' "sidebar-bottom" ]
                  [ navDownloadLink Routes.exportJson "⬇️" s.Shell.NavExportJson
                    navDownloadLink Routes.exportCsv "⬇️" s.Shell.NavExportCsv
                    navLink active Routes.settings "⚙️" s.Shell.NavSettings
                    if isAdmin then
                      navLink active Routes.members "👥" s.Shell.NavMembers ] ]
            Elem.div
              [ Attr.class' "content" ]
              [ Elem.main [ Attr.class' "container" ] content
                Elem.footer [ Attr.class' "container" ] [ Elem.small [] (versionFooter ()) ] ]
            // Re-runs on every body render (initial + hx-boost swaps) to sync the button label.
            Elem.script [ Attr.src "/theme-label.js" ] [] ] ]

  /// Minimal page shell for unauthenticated pages (login). No nav, no logout.
  let loginLayout (s: LocalizedStrings) (title: string) (content: XmlNode list) : XmlNode =
    Elem.html
      [ Attr.lang (Language.code s.Language) ]
      [ htmlHead title []
        Elem.body
          [ Attr.create "hx-boost" "false" ]
          [ themeToggleButton "theme-toggle--standalone"
            Elem.main
              [ Attr.class' "container login-container" ]
              ([ Elem.header
                   []
                   [ Elem.h1 [] [ Text.raw "BpMonitor" ]
                     Elem.p [] [ Text.raw s.Shell.AppTagline ] ] ]
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

  /// The shared form save/cancel row. `cancelHref` is the Cancel link destination.
  let formActions (s: LocalizedStrings) (cancelHref: string) : XmlNode =
    Elem.div
      [ Attr.class' "actions" ]
      [ Elem.button [ Attr.type' "submit" ] [ Text.raw s.Shell.Save ]
        Elem.a [ Attr.href cancelHref; Attr.role "button"; Attr.class' "secondary" ] [ Text.raw s.Shell.Cancel ] ]

  /// A single labeled form field: `<div class="field"><label/><input/></div>`.
  /// Shared by readingForm, memberForm, and settingsForm.
  let field (labelText: string) (name: string) (value: string) (inputType: string) : XmlNode =
    Elem.div
      [ Attr.class' "field" ]
      [ Elem.label [ Attr.for' name ] [ Text.raw labelText ]
        Elem.input [ Attr.type' inputType; Attr.id name; Attr.name name; Attr.value value ] ]

  /// The readings table's markup, with the container and each row's extra attributes
  /// left to the caller — e.g. tagging rows with `data-x` for chart-zoom syncing.
  let readingsTableWith
    (s: LocalizedStrings)
    (containerAttrs: XmlAttribute list)
    (rowAttrs: BloodPressureReading -> XmlAttribute list)
    (readings: BloodPressureReading list)
    : XmlNode =
    let header =
      Elem.thead
        []
        [ Elem.tr
            []
            [ Elem.th [ Attr.class' "col-timestamp" ] [ Text.raw s.Table.Timestamp ]
              Elem.th [ Attr.class' "col-center" ] [ Text.raw s.Table.Systolic ]
              Elem.th [ Attr.class' "col-center" ] [ Text.raw s.Table.Diastolic ]
              Elem.th [ Attr.class' "col-center" ] [ Text.raw s.Table.HeartRate ]
              Elem.th [] [ Text.raw s.Shell.Comment ]
              Elem.th [] [ Text.raw "" ] ] ]

    let row (r: BloodPressureReading) =
      Elem.tr
        (rowAttrs r)
        [ Elem.td [ Attr.class' "col-timestamp" ] [ Text.enc (Formats.formatLocal r.Timestamp) ]
          Elem.td [ Attr.class' "col-center" ] [ Text.enc (string r.Systolic) ]
          Elem.td [ Attr.class' "col-center" ] [ Text.enc (string r.Diastolic) ]
          Elem.td [ Attr.class' "col-center" ] [ Text.enc (string r.HeartRate) ]
          Elem.td [] [ Text.enc (r.Comments |> Option.defaultValue "") ]
          Elem.td
            [ Attr.class' "reading-actions" ]
            [ Elem.a
                [ Attr.href (Routes.readingEdit r.Id)
                  Attr.role "button"
                  Attr.class' "outline secondary" ]
                [ Text.raw s.Shell.Edit ] ] ]

    Elem.div containerAttrs [ Elem.table [] [ header; Elem.tbody [] (readings |> List.map row) ] ]

  /// The readings' table; wrapped in an id'd container so it can be targeted for
  /// partial swaps later.
  let readingsTable (s: LocalizedStrings) (readings: BloodPressureReading list) : XmlNode =
    readingsTableWith s [ Attr.id "readings" ] (fun _ -> []) readings
