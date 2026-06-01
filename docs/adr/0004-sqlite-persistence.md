# ADR-0004: SQLite Remains the Persistence Store (Accepted)

## Status

Accepted

## Context

[ADR-0003](0003-web-ui-frontend.md) added `BpMonitor.Web`, a web UI deployed to
the homelab VM. Introducing a long-running server that handles concurrent HTTP
requests naturally raises the question of whether persistence should move to a
networked client/server database (PostgreSQL, etc.), which is the conventional
default for web applications.

BpMonitor stores one person's blood pressure readings. It is a **single-user
application**: there is one writer, a small dataset, and no multi-tenant or
high-concurrency requirement. The data already lives in SQLite via EF Core, the
TUI uses it, and the web container keeps the database file on a mounted volume.

## Decision

**SQLite remains the persistence store.** We do not adopt a client/server
database.

Rationale:

- **Single user, modest data.** There is no concurrency or scale pressure that a
  networked database would relieve. SQLite comfortably handles a personal
  reading history.
- **The web layer is already concurrency-safe over SQLite.** Per ADR-0003 the
  `DbContext` is registered scoped (per request); SQLite's WAL mode handles the
  one-writer / occasional-reader pattern of a single user's browser and TUI.
- **Operational simplicity.** No separate database service to run, back up,
  secure, or patch on the homelab VM. Backup is copying one file from the mounted
  volume; the same file is portable between the TUI and the web container.
- **Zero new dependencies.** Keeps the minimal-dependency posture; `Data` keeps
  using `Microsoft.EntityFrameworkCore.Sqlite` and the manual `SchemaMigrations`
  module.

## Consequences

- Persistence stays a single SQLite file. Backups = copy the file; "migrating
  machines" = move the file.
- This is **not** suitable for multi-user or write-concurrent scenarios. If the
  application ever genuinely becomes multi-user (multiple independent writers),
  revisit this decision — EF Core makes swapping the provider feasible, but the
  manual `SchemaMigrations` approach (chosen because F# EF Core migrations are
  unsupported) would need rework for another provider.
- No change to existing code; this ADR records the deliberate choice so the web
  UI is not later misread as a reason to introduce a database server.
