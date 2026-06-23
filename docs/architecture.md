# Architecture: Blood Pressure Monitor

## Solution Structure

```text
code/
├── BpMonitor.slnx
├── BpMonitor.Core           # Domain models, interfaces, business logic
├── BpMonitor.Core.Tests     # Unit tests for Core
├── BpMonitor.Data           # EF Core + SQLite, repository implementations
├── BpMonitor.Data.Tests     # Integration tests for Data
├── BpMonitor.Charts         # Plotly.NET chart generation
├── BpMonitor.Charts.Tests   # Snapshot tests for Charts
├── BpMonitor.Export         # JSON and CSV serialisation and file write
├── BpMonitor.Export.Tests   # Tests for Export
├── BpMonitor.Web            # Falco web app (dashboard, add, history pages)
├── BpMonitor.Web.Tests      # Tests for Web layer
├── BpMonitor.Web.E2E.Tests  # Playwright .NET browser smoke tests (real out-of-process app + Chromium)
├── BpMonitor.Arch.Tests     # ArchUnit tests enforcing Clean Architecture rules
└── BpMonitor.TestSupport    # Shared test infrastructure (Verify snapshot settings) for *.Tests projects
```

## Tech Stack

| Concern | Decision |
| --- | --- |
| Solution format | `.slnx` (new XML-based format, VS 2022 17.10+) |
| Language / Runtime | F# on .NET |
| Web Framework | Falco 5 + Falco.Markup (server-rendered F# HTML) |
| Web interactivity | htmx (vendored, no build step) |
| Logging | Serilog.AspNetCore — structured CLEF JSON to stdout; `UseSerilogRequestLogging` for per-request lines; configured via `appsettings.json` `Serilog` section; captured by `docker logs` / `podman logs` / journald |
| Database | SQLite + EF Core |
| Charting | Plotly.NET — generates interactive HTML; plotly.js vendored locally (extracted from Plotly.NET's embedded resource via `scripts/extract-plotly-js.fsx`, no CDN) |
| Validation | `FsToolkit.ErrorHandling` — applicative validation with `Validation<'ok, 'err>` |
| Architecture | Clean Architecture (Core has zero dependencies on other projects) |
| Architecture tests | ArchUnit (via `BpMonitor.Arch.Tests`) |
| E2E tests | Playwright .NET (via `BpMonitor.Web.E2E.Tests`) — drives a real Chromium browser against a real out-of-process `BpMonitor.Web` instance with a fresh temp SQLite file |
| Test runner | xUnit v3 on Microsoft.Testing.Platform (MTP) — all 8 test projects run in parallel via `dotnet test` (default `--max-parallel-test-modules` = CPU count) |
| Test coverage | `Microsoft.Testing.Extensions.CodeCoverage` (18.0.6); run with `dotnet test -- --coverage --coverage-output-format cobertura`; outputs one GUID-named `.cobertura.xml` per project into `TestResults/` |

## Data Model

```fsharp
// BpMonitor.Core
type FamilyMember = {
    Id:           int
    Name:         string
    IsAdmin:      bool
    IsActive:     bool
    PasswordHash: string option   // None = unclaimed (no password set yet)
    Goal:         GoalRange       // per-member systolic/diastolic chart goal range
    CreatedAt:    DateTimeOffset
    ModifiedAt:   DateTimeOffset
}

type GoalRange = {
    SystolicMin:  int
    SystolicMax:  int
    DiastolicMin: int
    DiastolicMax: int
}
// GoalRange.defaults = { 90; 140; 60; 90 } — preset from Wegier et al. 2021
// (docs/resources/12911_2021_Article_1598.pdf, Fig. 3)

type BloodPressureReadingUnvalidated = {
    Systolic:  int
    Diastolic: int
    HeartRate: int
    Timestamp: DateTimeOffset
    Comments:  string option
}

type BloodPressureReading = {
    Id:         int
    MemberId:   int           // which family member this reading belongs to
    Systolic:   int
    Diastolic:  int
    HeartRate:  int
    Timestamp:  DateTimeOffset
    Comments:   string option
    CreatedAt:  DateTimeOffset
    ModifiedAt: DateTimeOffset
}

type ValidationError =
    | SystolicOutOfRange  of int
    | DiastolicOutOfRange of int
    | HeartRateOutOfRange of int
```

## Dependency Diagram

```mermaid
graph TD
    Core[BpMonitor.Core]
    Data[BpMonitor.Data]
    Export[BpMonitor.Export]
    Charts[BpMonitor.Charts]
    Web[BpMonitor.Web]

    Data --> Core
    Charts --> Core
    Export --> Core
    Web --> Core
    Web --> Data
    Web --> Charts
    Web --> Export
```

> **Note:** `Export` depends only on `Core` and is wired into `Web` to serve the `/export`
> (JSON) and `/export.csv` endpoints.

## Project Responsibilities

### BpMonitor.Core

- Domain models: `BloodPressureReading`, `BloodPressureReadingUnvalidated`, `FamilyMember`
- Repository interfaces: `IReadingRepository` (member-scoped), `IFamilyMemberRepository`
- `FamilyMember.hasActiveAdmin` — invariant: ≥1 member with `IsAdmin = true` and `IsActive = true`
- `FamilyMember.isClaimed` — true when `PasswordHash` is `Some`
- `GoalRange` — per-member systolic/diastolic chart goal range; `GoalRange.defaults` (90–140 / 60–90) and `GoalRange.create` (enforces min < max for each pair)
- `PasswordHashing` — PBKDF2-SHA256 hash/verify
- `ReadingStats` — date-window filter, AHA 2017 BP classification, windowed summary
- `DemoData` — deterministic Simpson-family fixture generator (fixed seed, ~5 years of readings)
- Applicative validation via `FsToolkit.ErrorHandling`; no dependencies on other projects

### BpMonitor.Data

- EF Core `DbContext`: `Readings` (`ReadingRecord`) and `Members` (`MemberRecord`)
- SQLite with WAL mode + 5 s busy timeout
- `IReadingRepository`: `EfReadingRepository` (filters by `MemberId`), `InMemoryReadingRepository`
- `IFamilyMemberRepository`: `EfFamilyMemberRepository`, `InMemoryFamilyMemberRepository`
- `SchemaMigrations.apply` — manual migrations (EF Core migrations don't support F#); `ensureActiveAdmin` promotes lowest-Id member when no active admin exists
- `DemoSeeder.seedIfEmpty` — seeds Simpson-family data (from `DemoData` in Core) when `BpMonitor:SeedDemoData=true` and the store is empty; idempotent

### BpMonitor.Charts

- Plotly.NET chart generation — `BpChart.toHtml goal readings` (history/recent line chart) and `BpChart.toHtmlDashed goal gran aggregated` (trends dashed chart)
- Returns a chart HTML fragment embedded directly into the page by the calling handler (`ReadingHandlers.fs`)
- `goal: GoalRange` renders a translucent horizontal background band per series (systolic mint `#008471`, diastolic cocoa `#9C652B`) behind the data, matching each series' line color — the "like-with-like" goal-range design from Wegier et al. 2021 (`docs/resources/12911_2021_Article_1598.pdf`, Fig. 3)
- Depends on Core only

### BpMonitor.Export

- JSON serialisation of `BloodPressureReading` lists (`JsonExport.serialize`, `JsonExport.tryWriteToFile`)
- CSV serialisation of `BloodPressureReading` lists (`CsvExport.serialize`, `CsvExport.tryWriteToFile`)
- Referenced by `BpMonitor.Web` to serve the `/export` (JSON) and `/export.csv` endpoints
- Depends on Core only

### BpMonitor.Web

- Falco web application on `0.0.0.0:5000`; references Core + Data + Charts + Export
- **Auth:** ASP.NET Core cookie auth; per-member PBKDF2-SHA256 password; unclaimed members set password on first login; cookie carries `NameIdentifier`/`Name`/`Role` claims
- **Isolation:** each member sees only their own readings; admins manage members via `/members` but not their readings
- **Routes:** `/` hub, `/add`, `/history`, `/recent`, `/trends`, `/settings`, `/members`, `/members/{id}/edit`, `/members/{id}/reset-password`, `/login`, `/login/{id}`, `POST /logout`
- **`/settings`:** self-service page where the logged-in member edits their own chart goal range (`GoalRange`); validated via `GoalRange.create` (min < max per pair)
- **`/recent`:** three rolling windows (last 7 / 14 / 30 days) of raw readings plus a 30-day chart — no aggregation. Above the chart, a Fig. 5-style (Wegier et al. 2021) "value strip" table lists every Systolic/Diastolic value in the chart's 30-day window in chronological order, sized to match the chart's rendered width with no horizontal scrolling. Each value is color-coded against the member's `GoalRange` (`GoalRange.classifySystolic`/`classifyDiastolic`): above the goal max renders orange, below the goal min renders blue, in-range stays neutral
- **`/trends`:** granularity selector (Weekly/Monthly/Yearly) + htmx-swapped period fragments; stats from `ReadingStats` (Core); `TimeProvider` injected for testability
- `protect` / `protectAdmin` combinators; active member resolved from `ClaimsPrincipal`
- Server-rendered HTML via `Falco.Markup`; htmx for partial updates; scoped `DbContext` per request
- Structured logging via Serilog (stdout → container/journal)
- **Version footer:** `Version.current` reads `AssemblyInformationalVersion`; shows `dev` when the value contains a `+` suffix (SDK default), `v1.2.3` for stamped releases

### BpMonitor.Arch.Tests

- ArchUnit rules enforcing Clean Architecture layer boundaries: Core ↛ Data/Web; Data ↛ Web; Charts ↛ Data/Web; Export ↛ Data/Charts/Web

## Design Principles

- Core is dependency-free to allow easy testing and future frontend swaps
- Each project has a single clear responsibility
- Best practices and longevity over shortcuts

## Development Tooling

[mise](https://mise.jdx.dev/) manages all non-dotnet linting tools for this project. The `mise.toml` at the repo root pins all tool versions; run `mise install` once after cloning to set up the local environment.

| Tool | Version source | Purpose |
| --- | --- | --- |
| node | `mise.toml` | Runtime for npm-based tools (markdownlint-cli2) |
| Biome | `mise.toml` | JS linter (`biome check`) for files in `wwwroot/` |
| markdownlint-cli2 | `mise.toml` | Markdown style linter |
| shellcheck | `mise.toml` | Shell script linter |

**Local usage:**

```bash
mise install          # install all pinned tools
mise run lint         # run all non-dotnet linters
mise run lint:md      # markdownlint only
mise run lint:js      # biome only
mise run lint:shell   # shellcheck only
mise exec -- biome check --write  # auto-fix safe JS issues
```

**CI:** the `lint-markdown`, `lint-js`, and `lint-shell` jobs in `.github/workflows/ci.yml` each install tools via `jdx/mise-action` and invoke the corresponding `mise run lint:*` task — the same command as local dev.

## Architecture Decision Records

See [docs/adr/](adr/) for records of significant architectural decisions, including abandoned spikes.
