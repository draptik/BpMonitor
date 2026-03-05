# Architecture: Blood Pressure Monitor

## Solution Structure

```text
BpMonitor.slnx
├── BpMonitor.Core        # Domain models, interfaces, business logic
├── BpMonitor.Data        # EF Core + SQLite, repository implementations
├── BpMonitor.Tui         # Terminal.Gui v2 app (data entry + navigation)
└── BpMonitor.Reports     # Plotly.NET chart generation → HTML output
```

## Tech Stack

| Concern | Decision |
| --- | --- |
| Solution format | `.slnx` (new XML-based format, VS 2022 17.10+) |
| Language / Runtime | .NET (C#) |
| TUI Framework | Terminal.Gui v2 |
| Database | SQLite + EF Core |
| Charts | Plotly.NET → interactive HTML, opens in default browser |
| Architecture | Clean Architecture (Core has zero dependencies on other projects) |
| CSV export | Implemented in `BpMonitor.Core` or `BpMonitor.Reports` |

## Data Model

```csharp
// BpMonitor.Core
public class BloodPressureReading
{
    public int Id { get; set; }
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int HeartRate { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Comments { get; set; }
}
```

## Project Responsibilities

### BpMonitor.Core
- Domain models
- Repository interfaces
- Business logic (e.g., validation, statistics)
- CSV export logic
- No dependencies on other projects

### BpMonitor.Data
- EF Core DbContext
- SQLite configuration
- Repository implementations
- Migrations

### BpMonitor.Tui
- Terminal.Gui v2 application
- Data entry form
- Navigation between views
- References Core + Data

### BpMonitor.Reports
- Plotly.NET chart definitions
- Default overview charts (trends over time)
- Interactive exploratory charts
- Opens generated HTML in default browser
- References Core + Data

## Future Extensions

- `BpMonitor.Api` — REST API for mobile data entry; plugs into Core + Data with no changes to existing projects

## Design Principles
- Core is dependency-free to allow easy testing and future frontend swaps
- Each project has a single clear responsibility
- Best practices and longevity over shortcuts
