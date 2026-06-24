namespace BpMonitor.Core

/// ISO-8601 week-numbering year + week (week 1..53).
/// NB: Year is the ISO week-numbering year, which can differ from the calendar
/// year near year boundaries (e.g., 29 Dec 2025 can belong to ISO week 2026-W01).
type IsoWeek = { Year: int; Week: int }

/// Calendar year + month (month 1..12).
type YearMonth = { Year: int; Month: int }

type Granularity =
  | Weekly
  | Monthly
  | Yearly

type TrendPeriod =
  { Granularity: Granularity
    Key: string // URL-safe: "2026-W24" | "2026-06" | "2026"
    Label: string // Display: "This Week" | "Last Week" | "CW 22" | "June 2026" | "2026"
    Start: System.DateTimeOffset // inclusive, local midnight
    EndExclusive: System.DateTimeOffset }

module IsoWeek =
  open System
  open System.Globalization

  let ofDate (d: DateTime) : IsoWeek =
    { Year = ISOWeek.GetYear(d)
      Week = ISOWeek.GetWeekOfYear(d) }

  let monday (w: IsoWeek) : DateTime =
    ISOWeek.ToDateTime(w.Year, w.Week, DayOfWeek.Monday)

module TrendPeriod =
  open System
  // ── private helpers ─────────────────────────────────────────────────────────

  let private localMidnight (date: DateTime) : DateTimeOffset =
    DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date))

  let private isoWeekKey (w: IsoWeek) = $"{w.Year}-W{w.Week:D2}"

  let private parseIsoWeekKey (key: string) : IsoWeek option =
    match key.Split('-') with
    | [| y; w |] when w.Length >= 2 && w[0] = 'W' ->
      match Int32.TryParse y, Int32.TryParse(w[1..]) with
      | (true, year), (true, week) when week >= 1 && week <= 53 -> Some { Year = year; Week = week }
      | _ -> None
    | _ -> None

  let private monthKey (ym: YearMonth) = $"{ym.Year}-{ym.Month:D2}"

  let private parseMonthKey (key: string) : YearMonth option =
    match key.Split('-') with
    | [| y; m |] ->
      match Int32.TryParse y, Int32.TryParse m with
      | (true, year), (true, month) when month >= 1 && month <= 12 -> Some { Year = year; Month = month }
      | _ -> None
    | _ -> None

  let private parseYearKey (key: string) : int option =
    match Int32.TryParse key with
    | true, year when year >= 1000 && year <= 9999 -> Some year
    | _ -> None

  let private previousIsoWeek (w: IsoWeek) : IsoWeek =
    let prevMonday = (IsoWeek.monday w).AddDays(-7.0)
    IsoWeek.ofDate prevMonday

  let private weekLabel (period: IsoWeek) (now: IsoWeek) : string =
    if period = now then
      "This Week"
    else
      let prev = previousIsoWeek now

      if period = prev then
        "Last Week"
      elif period.Year <> now.Year then
        $"CW {period.Week}/{period.Year}"
      else
        $"CW {period.Week}"

  let private monthLabel (ym: YearMonth) (now: DateTimeOffset) : string =
    let local = now.ToLocalTime()

    let nowYm =
      { Year = local.Year
        Month = local.Month }

    if ym = nowYm then
      "This Month"
    else
      let prevDate = DateTime(local.Year, local.Month, 1).AddMonths(-1)

      let prevYm =
        { Year = prevDate.Year
          Month = prevDate.Month }

      if ym = prevYm then
        "Last Month"
      else
        DateTime(ym.Year, ym.Month, 1).ToString("MMM yyyy")

  let private yearLabel (year: int) (now: DateTimeOffset) : string =
    let local = now.ToLocalTime()

    if year = local.Year then "This Year"
    elif year = local.Year - 1 then "Last Year"
    else string year

  // ── public API ──────────────────────────────────────────────────────────────

  let slug =
    function
    | Weekly -> "weekly"
    | Monthly -> "monthly"
    | Yearly -> "yearly"

  let parseGranularity (s: string) : Granularity option =
    match s.ToLowerInvariant() with
    | "weekly" -> Some Weekly
    | "monthly" -> Some Monthly
    | "yearly" -> Some Yearly
    | _ -> None

  let current (gran: Granularity) (now: DateTimeOffset) : TrendPeriod =
    let local = now.ToLocalTime()

    match gran with
    | Weekly ->
      let w = IsoWeek.ofDate local.Date
      let monday = IsoWeek.monday w

      { Granularity = Weekly
        Key = isoWeekKey w
        Label = "This Week"
        Start = localMidnight monday
        EndExclusive = localMidnight (monday.AddDays 7.0) }

    | Monthly ->
      let ym =
        { Year = local.Year
          Month = local.Month }

      let start = DateTime(ym.Year, ym.Month, 1)

      { Granularity = Monthly
        Key = monthKey ym
        Label = "This Month"
        Start = localMidnight start
        EndExclusive = localMidnight (start.AddMonths 1) }

    | Yearly ->
      let y = local.Year

      { Granularity = Yearly
        Key = string y
        Label = "This Year"
        Start = localMidnight (DateTime(y, 1, 1))
        EndExclusive = localMidnight (DateTime(y + 1, 1, 1)) }

  let ofKey (gran: Granularity) (key: string) (now: DateTimeOffset) : TrendPeriod option =
    let local = now.ToLocalTime()

    match gran with
    | Weekly ->
      parseIsoWeekKey key
      |> Option.map (fun w ->
        let monday = IsoWeek.monday w
        let nowW = IsoWeek.ofDate local.Date

        { Granularity = Weekly
          Key = key
          Label = weekLabel w nowW
          Start = localMidnight monday
          EndExclusive = localMidnight (monday.AddDays 7.0) })

    | Monthly ->
      parseMonthKey key
      |> Option.map (fun ym ->
        let start = DateTime(ym.Year, ym.Month, 1)

        { Granularity = Monthly
          Key = key
          Label = monthLabel ym now
          Start = localMidnight start
          EndExclusive = localMidnight (start.AddMonths 1) })

    | Yearly ->
      parseYearKey key
      |> Option.map (fun year ->
        { Granularity = Yearly
          Key = key
          Label = yearLabel year now
          Start = localMidnight (DateTime(year, 1, 1))
          EndExclusive = localMidnight (DateTime(year + 1, 1, 1)) })

  /// Fixed-window list of periods ending at the current one, in chronological order
  /// (oldest first, newest/current last). Window sizes: Weekly = 12, Monthly = 12, Yearly = 5.
  let available (gran: Granularity) (now: DateTimeOffset) : TrendPeriod list =
    let windowSize =
      match gran with
      | Weekly -> 12
      | Monthly -> 12
      | Yearly -> 5

    // Walk backwards from current, prepending each older period; result is oldest-first.
    let rec collect (p: TrendPeriod) (remaining: int) (acc: TrendPeriod list) =
      if remaining = 0 then
        acc
      else
        let prevKey = (current gran (p.Start.AddDays(-1.0))).Key

        match ofKey gran prevKey now with
        | None -> acc
        | Some prev -> collect prev (remaining - 1) (prev :: acc)

    let curr = current gran now
    collect curr (windowSize - 1) [ curr ]
