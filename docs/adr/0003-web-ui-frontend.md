# ADR-0003: Web UI Frontend (Accepted)

## Status

Accepted  
**Note:** The TUI mentioned throughout this ADR was removed in PR #151. `BpMonitor.Web`
is now the sole interface. References to "alongside the TUI" and "Two UIs" in the
Consequences section are historical; the current architecture has one UI only.

## Context

BpMonitor's only interface was the Terminal.Gui TUI, which runs on one machine
against a local SQLite file. We wanted a frontend that is reachable from a
browser — including a phone — against a single shared dataset.

[ADR-0001](0001-pwa-phone-client-spike.md) abandoned an earlier attempt at this:
a Bolero/Blazor **PWA**. The blocker there was never the UI — it was **sync**.
Keeping private health data in sync from a phone with *no always-on server*
proved unsolvable (Android's File System Access API does not persist permissions
across browser sessions, WebDAV was CORS-blocked, and off-infrastructure options
like Cloudflare Workers were rejected for private data). ADR-0001 concluded that
a phone-friendly frontend needs either a native app or **"a lightweight
always-on server that the PWA can reach directly."**

We now have exactly that: the web UI is **deployed to a VM in the homelab**.

## Decision

Add a new **`BpMonitor.Web`** project — a server-rendered web UI — alongside the
TUI. Both share `Core`/`Data`/`Charts`; the TUI is unchanged.

Key choices:

- **Server-rendered, not a SPA or WASM.** The app *holds* the data and renders
  HTML on the server, so the ADR-0001 sync problem simply does not exist — the
  browser is a thin client over a reachable server. No client-side data store,
  no sync.
- **[Falco](https://www.falcoframework.com/) + Falco.Markup + vendored
  [htmx](https://htmx.org/).** Keeps the stack 100% F# with a minimal dependency
  surface and **no JavaScript build chain**, matching the rest of the codebase.
  Falco is a thin layer over ASP.NET Core minimal APIs; views are F#
  (`Falco.Markup`); htmx is a single vendored static file. Giraffe was the main
  alternative — equally viable, but Falco's smaller footprint fit the project's
  minimalism. Bolero/Blazor WASM was rejected: it is client-heavy and is the
  approach ADR-0001 already found wanting for this use case.
- **Reuse, don't reinvent.** Reads/writes go through the existing
  `IReadingRepository`; validation reuses `BloodPressureReading.parse`; the
  dashboard chart reuses `BpChart.toHtml` (embedded in an `<iframe>` to isolate
  Plotly's scripts). The `DbContext` is registered **scoped** (per-request),
  which — unlike the TUI's single-context factory — is safe under concurrent
  web requests; schema migrations run once at startup.
- **Deployment: Podman on the homelab VM.** A multi-stage `Containerfile` plus
  systemd **Quadlet** units (`docs/example-deploy/`). The container binds `0.0.0.0:5000` and
  keeps SQLite on a mounted volume, so it is reachable on the LAN (incl. phones)
  and survives redeploys.

Scope for the first version is a dashboard (readings table + chart) and CRUD
(add / edit). Import (Markdown/JSON) is intentionally out of scope for now.

## Consequences

- A phone browser on the home network can now enter and view readings against
  the shared database — closing the loop ADR-0001 left open — without any of the
  sync machinery that doomed the PWA.
- A new architectural layer exists. Architecture tests (`BpMonitor.Arch.Tests`)
  enforce that `Core`/`Data`/`Charts`/`Export`/`Import` do **not** depend on
  `Web`; `Web` may only depend inward.
- The data is served **unauthenticated**. It is intended for a trusted LAN; if
  it ever needs wider exposure, it must be fronted by a reverse proxy / auth.
- CI requires no changes: `dotnet build`/`test` over `BpMonitor.slnx` pick up the
  new projects automatically. The container image is built on the VM (see
  `deploy/README.md`); no CI image-publish job was added.
- Two UIs (TUI + Web) now exist over the same `Core`/`Data`. If they diverge in
  behaviour, prefer extracting shared logic into `Core` rather than duplicating
  it (validation and range-loading already follow this pattern).
