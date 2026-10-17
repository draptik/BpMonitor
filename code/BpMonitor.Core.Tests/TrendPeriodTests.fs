module TrendPeriodTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

// Fixed "now" = Tuesday 2026-06-09 12:00 UTC (ISO week 24 of 2026, June 2026)
let private now = DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero)

// ── slug / parseGranularity ────────────────────────────────────────────────────

[<Fact>]
let ``slug Weekly roundtrips through parseGranularity`` () =
  test <@ TrendPeriod.parseGranularity (TrendPeriod.slug Weekly) = Some Weekly @>

[<Fact>]
let ``slug Monthly roundtrips through parseGranularity`` () =
  test <@ TrendPeriod.parseGranularity (TrendPeriod.slug Monthly) = Some Monthly @>

[<Fact>]
let ``slug Yearly roundtrips through parseGranularity`` () =
  test <@ TrendPeriod.parseGranularity (TrendPeriod.slug Yearly) = Some Yearly @>

[<Fact>]
let ``parseGranularity returns None for unknown string`` () =
  test <@ TrendPeriod.parseGranularity "daily" = None @>
  test <@ TrendPeriod.parseGranularity "" = None @>

// ── smart constructors ─────────────────────────────────────────────────────────

[<Fact>]
let ``CalendarYear.tryCreate accepts every year DateTime can represent`` () =
  test <@ CalendarYear.tryCreate 1 |> Option.map CalendarYear.value = Some 1 @>
  test <@ CalendarYear.tryCreate 9999 |> Option.map CalendarYear.value = Some 9999 @>

[<Fact>]
let ``CalendarYear.tryCreate rejects years outside 1-9999`` () =
  test <@ CalendarYear.tryCreate 0 = None @>
  test <@ CalendarYear.tryCreate 10000 = None @>

[<Fact>]
let ``IsoWeek.tryCreate accepts week 53 only in a 53-week year`` () =
  let year y = (CalendarYear.tryCreate y).Value
  test <@ (IsoWeek.tryCreate (year 2026) 53).IsSome @>
  test <@ IsoWeek.tryCreate (year 2025) 53 = None @>

[<Fact>]
let ``IsoWeek.tryCreate rejects week 0`` () =
  test <@ IsoWeek.tryCreate (CalendarYear.tryCreate 2026).Value 0 = None @>

[<Fact>]
let ``YearMonth.tryCreate rejects months outside 1-12`` () =
  let year = (CalendarYear.tryCreate 2026).Value
  test <@ (YearMonth.tryCreate year 12).IsSome @>
  test <@ YearMonth.tryCreate year 0 = None @>
  test <@ YearMonth.tryCreate year 13 = None @>

// ── current Weekly ─────────────────────────────────────────────────────────────

[<Fact>]
let ``current Weekly: key is ISO week format`` () =
  let p = TrendPeriod.current Weekly now
  test <@ p.Key = "2026-W24" @>

[<Fact>]
let ``current Weekly: label is 'This Week'`` () =
  let p = TrendPeriod.current Weekly now
  test <@ p.Label = ThisWeek @>

[<Fact>]
let ``current Weekly: Start is Monday midnight local`` () =
  // 2026-W24 starts on Monday 2026-06-08
  let p = TrendPeriod.current Weekly now
  let local = p.Start.ToLocalTime()
  let y, mo, d = local.Year, local.Month, local.Day
  let h, mi, s = local.Hour, local.Minute, local.Second
  test <@ y = 2026 && mo = 6 && d = 8 @>
  test <@ h = 0 && mi = 0 && s = 0 @>

[<Fact>]
let ``current Weekly: EndExclusive is 7 days after Start`` () =
  let p = TrendPeriod.current Weekly now
  let expected = p.Start.AddDays(7.0)
  test <@ p.EndExclusive = expected @>

[<Fact>]
let ``current Weekly: granularity field is Weekly`` () =
  let p = TrendPeriod.current Weekly now
  test <@ p.Granularity = Weekly @>

// ── current Monthly ────────────────────────────────────────────────────────────

[<Fact>]
let ``current Monthly: key is YYYY-MM format`` () =
  let p = TrendPeriod.current Monthly now
  test <@ p.Key = "2026-06" @>

[<Fact>]
let ``current Monthly: label is 'This Month'`` () =
  let p = TrendPeriod.current Monthly now
  test <@ p.Label = ThisMonth @>

[<Fact>]
let ``current Monthly: Start is first of month local midnight`` () =
  let p = TrendPeriod.current Monthly now
  let local = p.Start.ToLocalTime()
  let y, mo, d = local.Year, local.Month, local.Day
  let h, mi, s = local.Hour, local.Minute, local.Second
  test <@ y = 2026 && mo = 6 && d = 1 @>
  test <@ h = 0 && mi = 0 && s = 0 @>

[<Fact>]
let ``current Monthly: EndExclusive is first of next month`` () =
  let p = TrendPeriod.current Monthly now
  let endLocal = p.EndExclusive.ToLocalTime()
  let y, mo, d = endLocal.Year, endLocal.Month, endLocal.Day
  test <@ y = 2026 && mo = 7 && d = 1 @>

// ── current Yearly ─────────────────────────────────────────────────────────────

[<Fact>]
let ``current Yearly: key is YYYY format`` () =
  let p = TrendPeriod.current Yearly now
  test <@ p.Key = "2026" @>

[<Fact>]
let ``current Yearly: label is 'This Year'`` () =
  let p = TrendPeriod.current Yearly now
  test <@ p.Label = ThisYear @>

[<Fact>]
let ``current Yearly: Start is Jan 1 local midnight`` () =
  let p = TrendPeriod.current Yearly now
  let local = p.Start.ToLocalTime()
  let y, mo, d = local.Year, local.Month, local.Day
  test <@ y = 2026 && mo = 1 && d = 1 @>

[<Fact>]
let ``current Yearly: EndExclusive is Jan 1 of next year`` () =
  let p = TrendPeriod.current Yearly now
  let endLocal = p.EndExclusive.ToLocalTime()
  let y, mo, d = endLocal.Year, endLocal.Month, endLocal.Day
  test <@ y = 2027 && mo = 1 && d = 1 @>

// ── ofKey ─────────────────────────────────────────────────────────────────────

[<Fact>]
let ``ofKey Weekly: parses current week key and labels 'This Week'`` () =
  let p = TrendPeriod.ofKey Weekly "2026-W24" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = ThisWeek @>

[<Fact>]
let ``ofKey Weekly: parses previous week key and labels 'Last Week'`` () =
  // 2026-W24 starts 2026-06-08; previous is W23 (starts 2026-06-01)
  let p = TrendPeriod.ofKey Weekly "2026-W23" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = LastWeek @>

[<Fact>]
let ``ofKey Weekly: older same-year week uses CW label without year`` () =
  let p = TrendPeriod.ofKey Weekly "2026-W10" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = CalendarWeek 10 @>

[<Fact>]
let ``ofKey Weekly: older different-year week appends year`` () =
  let p = TrendPeriod.ofKey Weekly "2025-W50" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = CalendarWeekOfYear(50, 2025) @>

[<Fact>]
let ``ofKey Weekly: invalid key returns None`` () =
  test <@ TrendPeriod.ofKey Weekly "bad" now = None @>
  test <@ TrendPeriod.ofKey Weekly "2026-M10" now = None @>

[<Fact>]
let ``ofKey Weekly: week 0 returns None`` () =
  test <@ TrendPeriod.ofKey Weekly "2026-W00" now = None @>

[<Fact>]
let ``ofKey Weekly: week 54 returns None`` () =
  test <@ TrendPeriod.ofKey Weekly "2026-W54" now = None @>

[<Fact>]
let ``ofKey Weekly: week 53 of a 52-week year returns None`` () =
  test <@ TrendPeriod.ofKey Weekly "2025-W53" now = None @>

[<Fact>]
let ``ofKey Weekly: out-of-range year returns None`` () =
  test <@ TrendPeriod.ofKey Weekly "0-W01" now = None @>
  test <@ TrendPeriod.ofKey Weekly "9999-W52" now = None @>

[<Fact>]
let ``ofKey Monthly: parses current month key and labels 'This Month'`` () =
  let p = TrendPeriod.ofKey Monthly "2026-06" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = ThisMonth @>

[<Fact>]
let ``ofKey Monthly: parses previous month key and labels 'Last Month'`` () =
  let p = TrendPeriod.ofKey Monthly "2026-05" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = LastMonth @>

[<Fact>]
let ``ofKey Monthly: handles January-to-December year boundary for Last Month`` () =
  let jan2026 = DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)
  let p = TrendPeriod.ofKey Monthly "2025-12" jan2026
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = LastMonth @>

[<Fact>]
let ``ofKey Monthly: older month uses a MonthOfYear label`` () =
  let p = TrendPeriod.ofKey Monthly "2026-03" now
  test <@ p.IsSome @>
  test <@ p.Value.Label = MonthOfYear(3, 2026) @>

[<Fact>]
let ``ofKey Monthly: invalid key returns None`` () =
  test <@ TrendPeriod.ofKey Monthly "2026-13" now = None @>
  test <@ TrendPeriod.ofKey Monthly "abc" now = None @>

[<Fact>]
let ``ofKey Monthly: out-of-range year returns None`` () =
  test <@ TrendPeriod.ofKey Monthly "0-01" now = None @>
  test <@ TrendPeriod.ofKey Monthly "9999-12" now = None @>

[<Fact>]
let ``ofKey Yearly: parses current year key and labels 'This Year'`` () =
  let p = TrendPeriod.ofKey Yearly "2026" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = ThisYear @>

[<Fact>]
let ``ofKey Yearly: parses previous year key and labels 'Last Year'`` () =
  let p = TrendPeriod.ofKey Yearly "2025" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = LastYear @>

[<Fact>]
let ``ofKey Yearly: older year uses year as label`` () =
  let p = TrendPeriod.ofKey Yearly "2024" now
  test <@ p.IsSome @>
  let label = p.Value.Label
  test <@ label = Year 2024 @>

[<Fact>]
let ``ofKey Yearly: invalid key returns None`` () =
  test <@ TrendPeriod.ofKey Yearly "abc" now = None @>

[<Fact>]
let ``ofKey Yearly: year 9999 returns None`` () =
  test <@ TrendPeriod.ofKey Yearly "9999" now = None @>

// Hand-typed URLs: only the canonical key the app itself generates names a period, so
// the period pill whose key matches is always the one highlighted.
[<Theory>]
[<InlineData("2026-W5")>]
[<InlineData("2026-W+5")>]
[<InlineData(" 2026-W05")>]
[<InlineData("02026-W05")>]
let ``ofKey Weekly: a non-canonical spelling of a valid week returns None`` (key: string) =
  test <@ TrendPeriod.ofKey Weekly key now = None @>

[<Theory>]
[<InlineData("2026-6")>]
[<InlineData("2026- 6")>]
[<InlineData("+2026-06")>]
let ``ofKey Monthly: a non-canonical spelling of a valid month returns None`` (key: string) =
  test <@ TrendPeriod.ofKey Monthly key now = None @>

[<Theory>]
[<InlineData("+2026")>]
[<InlineData(" 2026")>]
[<InlineData("02026")>]
let ``ofKey Yearly: a non-canonical spelling of a valid year returns None`` (key: string) =
  test <@ TrendPeriod.ofKey Yearly key now = None @>

// A period starting on 0001-01-01 can't be built east of UTC: its local midnight is
// before DateTimeOffset.MinValue in UTC. Rejected regardless of the time zone.
[<Fact>]
let ``ofKey rejects periods that start on the first day DateTime can represent`` () =
  test <@ TrendPeriod.ofKey Yearly "1" now = None @>
  test <@ TrendPeriod.ofKey Monthly "1-01" now = None @>
  test <@ TrendPeriod.ofKey Weekly "1-W01" now = None @>

[<Fact>]
let ``ofKey still accepts the second week of year 1`` () =
  test <@ (TrendPeriod.ofKey Weekly "1-W02" now).IsSome @>

// ── available ─────────────────────────────────────────────────────────────────
// Fixed window: 12 weeks, 12 months, 5 years — always shown regardless of readings.
// Chronological order: oldest first (left), newest/current last (right).

[<Fact>]
let ``available Weekly: returns 12 periods`` () =
  let result = TrendPeriod.available Weekly now
  let len = result.Length
  test <@ len = 12 @>

[<Fact>]
let ``available Monthly: returns 12 periods`` () =
  let result = TrendPeriod.available Monthly now
  let len = result.Length
  test <@ len = 12 @>

[<Fact>]
let ``available Yearly: returns 5 periods`` () =
  let result = TrendPeriod.available Yearly now
  let len = result.Length
  test <@ len = 5 @>

[<Fact>]
let ``available Weekly: current (This Week) is last`` () =
  let result = TrendPeriod.available Weekly now
  let last = result |> List.last |> _.Label
  test <@ last = ThisWeek @>

[<Fact>]
let ``available Monthly: current (This Month) is last`` () =
  let result = TrendPeriod.available Monthly now
  let last = result |> List.last |> _.Label
  test <@ last = ThisMonth @>

[<Fact>]
let ``available Yearly: current (This Year) is last`` () =
  let result = TrendPeriod.available Yearly now
  let last = result |> List.last |> _.Label
  test <@ last = ThisYear @>

[<Fact>]
let ``available Weekly: second-to-last is Last Week`` () =
  let result = TrendPeriod.available Weekly now
  let secondLast = result |> List.item (result.Length - 2) |> _.Label
  test <@ secondLast = LastWeek @>

[<Fact>]
let ``available Monthly: second-to-last is Last Month`` () =
  let result = TrendPeriod.available Monthly now
  let secondLast = result |> List.item (result.Length - 2) |> _.Label
  test <@ secondLast = LastMonth @>

[<Fact>]
let ``available Weekly: periods are in chronological order`` () =
  let result = TrendPeriod.available Weekly now

  let isAscending =
    result |> List.pairwise |> List.forall (fun (a, b) -> a.Start < b.Start)

  test <@ isAscending @>

[<Fact>]
let ``available Monthly: periods are in chronological order`` () =
  let result = TrendPeriod.available Monthly now

  let isAscending =
    result |> List.pairwise |> List.forall (fun (a, b) -> a.Start < b.Start)

  test <@ isAscending @>

[<Fact>]
let ``available Yearly: includes 2024 even with no readings`` () =
  // Fixed window of 5 years back from 2026 includes 2022..2026; 2024 must be present
  let result = TrendPeriod.available Yearly now
  let has2024 = result |> List.exists (fun p -> p.Key = "2024")
  test <@ has2024 @>

[<Fact>]
let ``available: no duplicate keys`` () =
  let result = TrendPeriod.available Monthly now
  let keys = result |> List.map _.Key
  let uniqueCount = keys |> List.distinct |> List.length
  test <@ keys.Length = uniqueCount @>
