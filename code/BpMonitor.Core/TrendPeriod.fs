namespace BpMonitor.Core

/// A year DateTime can represent (1..9999).
type CalendarYear = private CalendarYear of int

module CalendarYear =
  let tryCreate (year: int) : CalendarYear option =
    if year >= 1 && year <= 9999 then
      Some(CalendarYear year)
    else
      None

  let value (CalendarYear year) = year

/// ISO-8601 week-numbering year + week (1..weeks in that year), valid by construction.
/// NB: Year is the ISO week-numbering year, which can differ from the calendar
/// year near year boundaries (e.g., 29 Dec 2025 can belong to ISO week 2026-W01).
type IsoWeek =
  private
    {
      Year: CalendarYear
      Week: int
    }

/// Calendar year + month (month 1..12), valid by construction.
type YearMonth =
  private
    {
      Year: CalendarYear
      Month: int
    }

type Granularity =
  | Weekly
  | Monthly
  | Yearly

/// A period's display label, structured so rendering (and translation) happens at the
/// view layer via `LocalizedStrings.Trend`, not as English prose baked into the domain.
type PeriodLabel =
  | ThisWeek
  | LastWeek
  | CalendarWeek of week: int
  | CalendarWeekOfYear of week: int * year: int
  | ThisMonth
  | LastMonth
  | MonthOfYear of month: int * year: int
  | ThisYear
  | LastYear
  | Year of int

type TrendPeriod =
  {
    Granularity: Granularity
    Key: string // URL-safe: "2026-W24" | "2026-06" | "2026"
    Label: PeriodLabel
    Start: System.DateTimeOffset // inclusive, local midnight
    EndExclusive: System.DateTimeOffset
  }

module IsoWeek =
  open System
  open System.Globalization

  let tryCreate (year: CalendarYear) (week: int) : IsoWeek option =
    if week >= 1 && week <= ISOWeek.GetWeeksInYear(CalendarYear.value year) then
      Some { Year = year; Week = week }
    else
      None

  // Every DateTime falls in an ISO year within 1..9999, so no validation is needed here.
  let ofDate (d: DateTime) : IsoWeek =
    {
      Year = CalendarYear(ISOWeek.GetYear d)
      Week = ISOWeek.GetWeekOfYear d
    }

  let monday (w: IsoWeek) : DateTime =
    ISOWeek.ToDateTime(CalendarYear.value w.Year, w.Week, DayOfWeek.Monday)

module YearMonth =
  open System

  let tryCreate (year: CalendarYear) (month: int) : YearMonth option =
    if month >= 1 && month <= 12 then
      Some { Year = year; Month = month }
    else
      None

  let ofDate (d: DateTime) : YearMonth =
    {
      Year = CalendarYear d.Year
      Month = d.Month
    }

  let firstDay (ym: YearMonth) : DateTime =
    DateTime(CalendarYear.value ym.Year, ym.Month, 1)

module TrendPeriod =
  open System
  // ── private helpers ─────────────────────────────────────────────────────────

  let private localMidnight (date: DateTime) : DateTimeOffset =
    DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date))

  let private isoWeekKey (w: IsoWeek) =
    $"{CalendarYear.value w.Year}-W{w.Week:D2}"

  /// Matches a string that parses as an int.
  let private (|Int|_|) (s: string) : int option =
    match Int32.TryParse s with
    | true, v -> Some v
    | _ -> None

  /// Matches a string that parses as a year DateTime can represent.
  let private (|YearText|_|) (s: string) : CalendarYear option =
    match s with
    | Int year -> CalendarYear.tryCreate year
    | _ -> None

  let private parseIsoWeekKey (key: string) : IsoWeek option =
    match key.Split('-') with
    | [| YearText year; w |] when w.Length >= 2 && w[0] = 'W' ->
      match w[1..] with
      | Int week -> IsoWeek.tryCreate year week
      | _ -> None
    | _ -> None

  let private monthKey (ym: YearMonth) =
    $"{CalendarYear.value ym.Year}-{ym.Month:D2}"

  let private parseMonthKey (key: string) : YearMonth option =
    match key.Split('-') with
    | [| YearText year; Int month |] -> YearMonth.tryCreate year month
    | _ -> None

  let private parseYearKey (key: string) : CalendarYear option =
    match key with
    | YearText year -> Some year
    | _ -> None

  /// Whether a period starting at `start` ends within DateTime (fails only in year 9999).
  let private endFits (stepBack: DateTime -> DateTime) (start: DateTime) =
    start <= stepBack DateTime.MaxValue.Date

  let private previousIsoWeek (w: IsoWeek) : IsoWeek =
    let prevMonday = (IsoWeek.monday w).AddDays(-7.0)
    IsoWeek.ofDate prevMonday

  let private weekLabel (period: IsoWeek) (now: IsoWeek) : PeriodLabel =
    if period = now then
      ThisWeek
    else
      let prev = previousIsoWeek now

      if period = prev then
        LastWeek
      elif period.Year <> now.Year then
        CalendarWeekOfYear(period.Week, CalendarYear.value period.Year)
      else
        CalendarWeek period.Week

  let private monthLabel (ym: YearMonth) (now: DateTimeOffset) : PeriodLabel =
    let local = now.ToLocalTime()

    let nowYm = YearMonth.ofDate local.Date

    if ym = nowYm then
      ThisMonth
    elif ym = YearMonth.ofDate ((YearMonth.firstDay nowYm).AddMonths(-1)) then
      LastMonth
    else
      MonthOfYear(ym.Month, CalendarYear.value ym.Year)

  let private yearLabel (calendarYear: CalendarYear) (now: DateTimeOffset) : PeriodLabel =
    let local = now.ToLocalTime()
    let year = CalendarYear.value calendarYear

    if year = local.Year then ThisYear
    elif year = local.Year - 1 then LastYear
    else Year year

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

      {
        Granularity = Weekly
        Key = isoWeekKey w
        Label = ThisWeek
        Start = localMidnight monday
        EndExclusive = localMidnight (monday.AddDays 7.0)
      }

    | Monthly ->
      let ym = YearMonth.ofDate local.Date
      let start = YearMonth.firstDay ym

      {
        Granularity = Monthly
        Key = monthKey ym
        Label = ThisMonth
        Start = localMidnight start
        EndExclusive = localMidnight (start.AddMonths 1)
      }

    | Yearly ->
      let y = local.Year

      {
        Granularity = Yearly
        Key = string y
        Label = ThisYear
        Start = localMidnight (DateTime(y, 1, 1))
        EndExclusive = localMidnight (DateTime(y + 1, 1, 1))
      }

  let ofKey (gran: Granularity) (key: string) (now: DateTimeOffset) : TrendPeriod option =
    let local = now.ToLocalTime()

    match gran with
    | Weekly ->
      parseIsoWeekKey key
      |> Option.filter (IsoWeek.monday >> endFits _.AddDays(-7.0))
      |> Option.map (fun w ->
        let monday = IsoWeek.monday w
        let nowW = IsoWeek.ofDate local.Date

        {
          Granularity = Weekly
          Key = key
          Label = weekLabel w nowW
          Start = localMidnight monday
          EndExclusive = localMidnight (monday.AddDays 7.0)
        })

    | Monthly ->
      parseMonthKey key
      |> Option.filter (YearMonth.firstDay >> endFits _.AddMonths(-1))
      |> Option.map (fun ym ->
        let start = YearMonth.firstDay ym

        {
          Granularity = Monthly
          Key = key
          Label = monthLabel ym now
          Start = localMidnight start
          EndExclusive = localMidnight (start.AddMonths 1)
        })

    | Yearly ->
      parseYearKey key
      |> Option.filter (fun y -> endFits _.AddYears(-1) (DateTime(CalendarYear.value y, 1, 1)))
      |> Option.map (fun calendarYear ->
        let year = CalendarYear.value calendarYear

        {
          Granularity = Yearly
          Key = key
          Label = yearLabel calendarYear now
          Start = localMidnight (DateTime(year, 1, 1))
          EndExclusive = localMidnight (DateTime(year + 1, 1, 1))
        })

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
