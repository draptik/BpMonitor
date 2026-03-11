# Architecture: Blood Pressure Monitor

## Solution Structure

```text
code/
├── BpMonitor.slnx
├── BpMonitor.Core           # Domain models, interfaces, business logic
├── BpMonitor.Core.Tests     # Unit tests for Core
├── BpMonitor.Data           # EF Core + SQLite, repository implementations
├── BpMonitor.Data.Tests     # Integration tests for Data
├── BpMonitor.Import         # Markdown parser and import logic
├── BpMonitor.Import.Tests   # Unit tests for Import
├── BpMonitor.Charts         # Plotly.NET chart generation
├── BpMonitor.Charts.Tests   # Snapshot tests for Charts
├── BpMonitor.Tui            # Terminal.Gui v2 app (data entry + list view + import)
├── BpMonitor.Tui.Tests      # Tests for TUI layer
└── BpMonitor.ArchTests      # ArchUnit tests enforcing Clean Architecture rules
```

## Tech Stack

| Concern | Decision |
| --- | --- |
| Solution format | `.slnx` (new XML-based format, VS 2022 17.10+) |
| Language / Runtime | F# on .NET |
| TUI Framework | Terminal.Gui v2 |
| Database | SQLite + EF Core |
| Charting | Plotly.NET — generates interactive HTML, opens in default browser |
| Validation | `FsToolkit.ErrorHandling` — applicative validation with `Validation<'ok, 'err>` |
| Architecture | Clean Architecture (Core has zero dependencies on other projects) |
| Architecture tests | ArchUnit (via `BpMonitor.ArchTests`) |

## Data Model

```fsharp
// BpMonitor.Core
type BloodPressureReadingUnvalidated = {
    Systolic:  int
    Diastolic: int
    HeartRate: int
    Timestamp: DateTimeOffset
    Comments:  string option
}

type BloodPressureReading = {
    Id:         int
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

## Project Responsibilities

### BpMonitor.Core

- Domain models (`BloodPressureReading`, `BloodPressureReadingUnvalidated`)
- Repository interface (`IReadingRepository`)
- Business logic: applicative validation via `FsToolkit.ErrorHandling`
- No dependencies on other projects

### BpMonitor.Data

- EF Core `DbContext` and `ReadingRecord` entity
- SQLite configuration (`appsettings.json`)
- `IReadingRepository` implementations: `EfReadingRepository`, `InMemoryReadingRepository`
- Manual schema migrations via `SchemaMigrations` module (EF Core migrations do not support F#)
- `ReadingRepositoryFactory` wiring

### BpMonitor.Import

- Parses blood pressure readings from Markdown files (`parseMarkdown`, `parseLine`)
- Upsert import logic with summary (`ImportSummary`: added, updated, failed counts)
- Depends on Core only

### BpMonitor.Charts

- Plotly.NET chart generation (`BpChart.toHtml`)
- Produces a self-contained interactive HTML file opened in the default browser
- Depends on Core only

### BpMonitor.Tui

- Terminal.Gui v2 application
- Readings list view with Add (`N`), Edit (`Enter`), Chart (`C`), Import (`I`) keybindings
- Delegates to Core for validation, Data for persistence, Import for file import, Charts for visualisation
- References Core + Data + Import + Charts

### BpMonitor.ArchTests

- ArchUnit rules enforcing Clean Architecture layer boundaries
- Core must not depend on Data, Tui
- Data must not depend on Tui
- Import must not depend on Data, Tui, Charts
- Charts must not depend on Data, Tui

## Design Principles

- Core is dependency-free to allow easy testing and future frontend swaps
- Each project has a single clear responsibility
- Best practices and longevity over shortcuts
