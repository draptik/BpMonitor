# ADR 0013 — One deployment-wide time zone, passed explicitly

**Date:** 2026-10-17  
**Status:** Rejected (2026-10-17)

> **Why rejected:** every user of a deployment shares one time zone, so the host's zone (`TZ`)
> is the right one and the app keeps reading it directly. The testing gap this ADR described
> isn't worth the change across Core, Charts and Web; #606 already made the existing tests
> independent of the machine's zone. The implementation PR (#613) was closed unmerged.

## Context

"Local time" decides which day, week and month a reading belongs to, where trend periods start,
how timestamps are shown and typed, and where charts draw their points. Today every one of those
places asks the host directly: `TimeZoneInfo.Local`, `DateTimeOffset.ToLocalTime()` or
`TimeProvider.GetLocalNow()`. That's about 35 call sites across Core, Charts, Web and Data, plus
one hidden one: `Binding` reads a typed `2026-05-01 09:00` in the host's zone.

In production that works: the example deploy files set `TZ=Europe/Berlin`. Tests are another
matter:

- The suite runs in whatever zone the machine has. Nine tests silently assumed a zone near UTC
  until #606 rewrote their data with `Timestamp.local`; nothing stops the next one.
- No test can cover a DST switch, because a test can't choose the zone. Code that builds "local
  midnight" or groups by local day is exactly where DST bugs live, and it's untested.

BpMonitor is a household app: everyone using one deployment lives in the same time zone.

## Decision

- **One zone per deployment.** No per-member zones and no new setting. The zone stays whatever
  the host says (`TZ` in the container), read through `TimeProvider.LocalTimeZone`; the
  registered `TimeProvider.System` returns `TimeZoneInfo.Local`.
- **Passed explicitly, never looked up.** Core and Charts functions that need local time take a
  `zone: TimeZoneInfo` parameter, the same way views take `LocalizedStrings` (ADR 0008):
  `Formats.formatLocal zone ts`, `ReadingStats.aggregate zone gran readings`,
  `TrendPeriod.current zone gran now`, `Timestamp.local zone …`, and so on. Web handlers get it
  once per request (`HandlerHelpers.zone ctx = (timeProvider ctx).LocalTimeZone`) and hand it
  down. `DemoSeeder` already receives the `TimeProvider`.
- **Guarded.** An architecture test fails if production code calls `TimeZoneInfo.Local`,
  `DateTimeOffset.ToLocalTime()` or `TimeProvider.GetLocalNow()` outside the composition root,
  so a new call site can't quietly reintroduce the host dependency.
- **Tests pin the zone.** Unit tests pass a fixed zone (`Europe/Berlin`, which has DST) instead
  of the machine's; handler tests set it with `FakeTimeProvider.SetLocalTimeZone`. The suite then
  gives the same result on any machine, and DST cases become ordinary tests.
- **Typed times on DST days** (`Binding`):
  - A time that doesn't exist (the spring-forward gap, e.g. `2026-03-29 02:30` in Berlin) is
    rejected with a form error naming the time.
  - A time that happens twice (the fall-back overlap, e.g. `2026-10-25 02:30`) resolves to the
    earlier of the two instants (the summer-time offset).
- **Startup log** names the resolved zone (`Using time zone Europe/Berlin`). A host without zone
  data silently falls back to UTC even with `TZ` set; the log line makes that visible.

### Rollout

Three PRs, each green on its own. A signature change in Core or Charts can't compile until its
callers change too, so each PR updates downstream callers to pass a temporary
`TimeZoneInfo.Local`; the next PR replaces that with the request's zone.

1. Core: `Formats`, `Timestamp.local`, `ReadingStats`, `TrendPeriod`, plus the architecture test
   scoped to Core, and DST tests for grouping and period boundaries.
2. Charts: day maths for gaps and LOWESS, and every `formatLocal` call.
3. Web and Data: handlers, views, `Binding` with the DST input rule, `DemoSeeder`, the startup
   log line, and the architecture test widened to every production project.

## Alternatives considered

- **Keep asking the host; add DST tests that run only under a matching `TZ`** in an extra CI
  step. No code change, but the tests skip themselves on most machines, and the workflow change
  can't be pushed with the current token. Rejected.
- **A `BpMonitor:TimeZone` setting.** Frees the app from the host's `TZ` and zone data, but adds
  a setting nobody has asked for; with this ADR in place it's a small follow-up if it's ever
  needed. Deferred.
- **Per-member zones.** Members of one household share a zone. Rejected.
- **An ambient, mutable "current zone"** set at startup and overridden in tests. Tests run in
  parallel, so a process-wide setting would leak between them. Rejected.

## Consequences

- Many signatures gain a `zone` parameter, and test call sites change with them; the churn is
  mechanical but wide, hence the three PRs.
- Every place that formats a time for Plotly gets its zone from the same request value, so the
  BP chart, the medications timeline and the value strip can't disagree by an hour.
- Typing a nonexistent time on a spring-forward day now shows an error instead of being stored
  an hour off.
- The host still decides the zone in production; deploy docs keep `TZ=…`.
