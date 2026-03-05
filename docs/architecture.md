# Architecture: Blood Pressure Monitor

## Solution Structure

```text
BpMonitor.slnx
├── BpMonitor.Core           # Domain models, interfaces, business logic
├── BpMonitor.Core.Tests     # Unit tests for Core
├── BpMonitor.Data           # EF Core + SQLite, repository implementations
├── BpMonitor.Data.Tests     # Integration tests for Data
├── BpMonitor.Tui            # Terminal.Gui v2 app (data entry + list view)
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
    Id:        int
    Systolic:  int
    Diastolic: int
    HeartRate: int
    Timestamp: DateTimeOffset
    Comments:  string option
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
- EF Core `DbContext`
- SQLite configuration (`appsettings.json`)
- `IReadingRepository` implementation
- Migrations

### BpMonitor.Tui
- Terminal.Gui v2 application
- Data entry form with validation feedback
- Readings list view
- References Core + Data

### BpMonitor.ArchTests
- ArchUnit rules enforcing Clean Architecture layer boundaries

## Future Extensions

- `BpMonitor.Reports` — Plotly.NET chart generation → interactive HTML, opens in default browser
- `BpMonitor.Api` — REST API for mobile data entry; plugs into Core + Data with no changes to existing projects

## Design Principles
- Core is dependency-free to allow easy testing and future frontend swaps
- Each project has a single clear responsibility
- Best practices and longevity over shortcuts
