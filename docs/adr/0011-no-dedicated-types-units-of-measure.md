# ADR-0011: No Dedicated Types or Units-of-Measure for Systolic/Diastolic/HeartRate (Rejected)

## Status

Rejected

## Context

`Systolic`, `Diastolic`, and `HeartRate` are plain `int` fields throughout `BpMonitor.Core`
(`BloodPressureReading.fs`, `GoalRange.fs`, `ReadingStats.fs`, `DemoData.fs`). F# offers two
mechanisms that would make these harder to confuse with each other or with an arbitrary `int`:

- **Dedicated single-case wrapper types**, e.g. `type Systolic = Systolic of int`.
- **Units of measure** (F#'s `[<Measure>]` feature), e.g. `int<mmHg>` / `int<bpm>`.

Both were considered as a way to make it impossible to, say, pass a diastolic value where a
systolic one is expected, or accidentally do arithmetic mixing mmHg and bpm.

## Finding

Neither payoff materializes here:

- **Only one unit of measure exists.** Systolic, diastolic, and mean arterial pressure are all
  mmHg; heart rate is bpm. There's no mmol/L-vs-mg/dL style conversion, no metric/imperial
  split, no computation that mixes units — the one place two different units could ever meet is
  a heart-rate value flowing into a pressure field, which is already a distinct domain concept,
  not a units bug. Units of measure earn their keep when a codebase does arithmetic across
  units and a compile error catches a wrong conversion; there's no such arithmetic here.
- **The systolic/diastolic mix-up risk is real but narrow, and already guarded.** All three
  values enter the system through exactly one path — form parsing in
  `BloodPressureReadingUnvalidated` → `BloodPressureReading.validate` — which binds each field
  by name (`_.Systolic`, `_.Diastolic`, `_.HeartRate`) against its own range
  (`SystolicOutOfRange`, `DiastolicOutOfRange`, `HeartRateOutOfRange`) and rejects nonsense
  early (e.g. diastolic > systolic is already a domain rule, not something a wrapper type would
  add). Past that boundary, the fields never get reassigned to each other — they flow straight
  through as named record fields, and F#'s record field names already prevent
  `{ Systolic = r.Diastolic }`-style typos from compiling silently the way positional
  tuples or a shared primitive type would invite.
- **The types are structurally record fields, not loose values passed around positionally.**
  `Systolic: int` on a record is already labeled at every call site and at every function
  signature that destructures the record (`(reading: BloodPressureReading)`, not three bare
  ints). A wrapper type's main benefit — stopping positional argument transposition — doesn't
  apply to a codebase that doesn't pass these as bare positional `int` arguments.
- **Real cost, not just typing overhead.** Wrapper types or `[<Measure>]` would ripple into
  every layer that touches these fields: EF Core value converters in `BpMonitor.Data`, JSON/CSV
  (de)serialization in `BpMonitor.Export`, Plotly.NET series construction in `BpMonitor.Charts`,
  range validation and arithmetic (averages, LOWESS smoothing in `Lowess.fs`) in `Core`, and
  every Verify snapshot that currently renders these as plain numbers. Units of measure in
  particular don't survive serialization boundaries for free — every EF/JSON/CSV crossing would
  need an explicit strip/reapply, for a codebase where the field name already carries the
  meaning.

## Decision

**Not pursued.** `Systolic`, `Diastolic`, and `HeartRate` stay plain `int` record fields. The
mix-up risk they're meant to guard against is already closed by record field naming plus
`validate`'s per-field range checks at the one ingestion boundary; there is no cross-unit
arithmetic anywhere in the domain for units of measure to protect.

## Consequences

- No wrapper types or `[<Measure>]` annotations anywhere in `BpMonitor.Core`; `int` stays the
  type for all three fields across every layer.
- If the domain ever gains a second unit system (e.g. importing readings in a different
  pressure unit, or a metric that's computed by combining systolic/diastolic/heart-rate values
  in a way that could silently swap units), revisit this — that's the condition
  [`[<Measure>]`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure)
  is designed for, and it isn't the current shape of this codebase.
- If a bug ever surfaces from a systolic/diastolic transposition that record-field naming and
  `validate` failed to catch, that's new evidence this decision should be revisited — none has
  occurred so far.
