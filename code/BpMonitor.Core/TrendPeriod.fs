namespace BpMonitor.Core

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

module TrendPeriod =
  open System
  open System.Globalization

  // ── private helpers ─────────────────────────────────────────────────────────

  let private localMidnight (date: DateTime) : DateTimeOffset =
    DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date))

  let private isoWeekKey (isoYear: int) (week: int) = $"{isoYear}-W{week:D2}"

  let private parseIsoWeekKey (key: string) : (int * int) option =
    match key.Split('-') with
    | [| y; w |] when w.Length >= 2 && w.[0] = 'W' ->
      match Int32.TryParse y, Int32.TryParse(w.[1..]) with
      | (true, year), (true, week) when week >= 1 && week <= 53 -> Some(year, week)
      | _ -> None
    | _ -> None

  let private monthKey (year: int) (month: int) = $"{year}-{month:D2}"

  let private parseMonthKey (key: string) : (int * int) option =
    match key.Split('-') with
    | [| y; m |] ->
      match Int32.TryParse y, Int32.TryParse m with
      | (true, year), (true, month) when month >= 1 && month <= 12 -> Some(year, month)
      | _ -> None
    | _ -> None

  let private parseYearKey (key: string) : int option =
    match Int32.TryParse key with
    | true, year when year >= 1000 && year <= 9999 -> Some year
    | _ -> None

  let private previousIsoWeek (isoYear: int) (week: int) : int * int =
    let monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday)
    let prevMonday = monday.AddDays(-7.0)
    ISOWeek.GetYear(prevMonday), ISOWeek.GetWeekOfYear(prevMonday)

  let private weekLabel (isoYear: int) (week: int) (nowIsoYear: int) (nowWeek: int) : string =
    if isoYear = nowIsoYear && week = nowWeek then
      "This Week"
    else
      let prevIsoYear, prevWeek = previousIsoWeek nowIsoYear nowWeek

      if isoYear = prevIsoYear && week = prevWeek then "Last Week"
      elif isoYear <> nowIsoYear then $"CW {week}/{isoYear}"
      else $"CW {week}"

  let private monthLabel (year: int) (month: int) (now: DateTimeOffset) : string =
    let local = now.ToLocalTime()

    if year = local.Year && month = local.Month then
      "This Month"
    else
      let prevDate = DateTime(local.Year, local.Month, 1).AddMonths(-1)

      if year = prevDate.Year && month = prevDate.Month then
        "Last Month"
      else
        DateTime(year, month, 1).ToString("MMM yyyy")

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
      let isoYear = ISOWeek.GetYear(local.Date)
      let week = ISOWeek.GetWeekOfYear(local.Date)
      let monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday)

      { Granularity = Weekly
        Key = isoWeekKey isoYear week
        Label = "This Week"
        Start = localMidnight monday
        EndExclusive = localMidnight (monday.AddDays 7.0) }

    | Monthly ->
      let y, m = local.Year, local.Month
      let start = DateTime(y, m, 1)

      { Granularity = Monthly
        Key = monthKey y m
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
      |> Option.map (fun (isoYear, week) ->
        let monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday)
        let nowIsoYear = ISOWeek.GetYear(local.Date)
        let nowWeek = ISOWeek.GetWeekOfYear(local.Date)

        { Granularity = Weekly
          Key = key
          Label = weekLabel isoYear week nowIsoYear nowWeek
          Start = localMidnight monday
          EndExclusive = localMidnight (monday.AddDays 7.0) })

    | Monthly ->
      parseMonthKey key
      |> Option.map (fun (year, month) ->
        let start = DateTime(year, month, 1)

        { Granularity = Monthly
          Key = key
          Label = monthLabel year month now
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
  let available (gran: Granularity) (now: DateTimeOffset) (_readings: BloodPressureReading list) : TrendPeriod list =
    let windowSize =
      match gran with
      | Weekly -> 12
      | Monthly -> 12
      | Yearly -> 5

    // Collect periods going backwards from current, then reverse for chronological order.
    // acc head is always the most recently collected period (starts at current).
    let rec collect (p: TrendPeriod) (remaining: int) (acc: TrendPeriod list) =
      if remaining = 0 then
        acc // acc = [curr, prev, prev-1, ..., oldest] — reverse below
      else
        let prevKey = (current gran (p.Start.AddDays(-1.0))).Key

        match ofKey gran prevKey now with
        | None -> acc
        | Some prev -> collect prev (remaining - 1) (prev :: acc)

    let curr = current gran now
    // Build [oldest, ..., prev, curr] — collect walks backwards then we get oldest-first naturally
    // because each older period is prepended.
    // acc = [oldest, ..., prev, curr] because each older period is prepended
    collect curr (windowSize - 1) [ curr ]
