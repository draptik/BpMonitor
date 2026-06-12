namespace BpMonitor.Core

/// A period-averaged reading together with the number of raw readings that were
/// folded into it. Used by the trends chart to distinguish single-reading periods
/// (circle marker) from multi-reading averages (diamond marker).
type AggregatedReading =
  { Reading: BloodPressureReading
    Count: int
    MinSystolic: int
    MaxSystolic: int
    MinDiastolic: int
    MaxDiastolic: int }

type WindowSummary =
  { Granularity: Granularity
    PeriodKey: string
    Label: string
    Count: int
    MinSystolic: int
    AvgSystolic: int
    MaxSystolic: int
    MinDiastolic: int
    AvgDiastolic: int
    MaxDiastolic: int
    MinHeartRate: int
    AvgHeartRate: int
    MaxHeartRate: int }

module ReadingStats =
  open System
  open System.Globalization

  // ── filtering ────────────────────────────────────────────────────────────────

  /// Returns readings whose Timestamp falls within [startIncl, endExcl).
  let between
    (startIncl: DateTimeOffset)
    (endExcl: DateTimeOffset)
    (readings: BloodPressureReading list)
    : BloodPressureReading list =
    readings
    |> List.filter (fun r -> r.Timestamp >= startIncl && r.Timestamp < endExcl)

  // ── aggregation ──────────────────────────────────────────────────────────────

  /// Groups readings by local calendar date and returns one averaged reading per day
  /// together with the raw reading count, sorted ascending.
  /// The returned reading's Timestamp is midnight of that local date.
  /// Comments are dropped (not meaningful for averages).
  let private dailyAveragesWithCount (readings: BloodPressureReading list) : AggregatedReading list =
    readings
    |> List.groupBy (fun r -> r.Timestamp.ToLocalTime().Date)
    |> List.sortBy fst
    |> List.map (fun (date, rs) ->
      let n = List.length rs
      let offset = TimeZoneInfo.Local.GetUtcOffset(date)

      { Reading =
          { Id = 0
            MemberId = 0
            Systolic = rs |> List.sumBy _.Systolic |> (fun s -> s / n)
            Diastolic = rs |> List.sumBy _.Diastolic |> (fun s -> s / n)
            HeartRate = rs |> List.sumBy _.HeartRate |> (fun s -> s / n)
            Timestamp = DateTimeOffset(date, offset)
            Comments = None
            CreatedAt = DateTimeOffset.MinValue
            ModifiedAt = DateTimeOffset.MinValue }
        Count = n
        MinSystolic = rs |> List.minBy _.Systolic |> _.Systolic
        MaxSystolic = rs |> List.maxBy _.Systolic |> _.Systolic
        MinDiastolic = rs |> List.minBy _.Diastolic |> _.Diastolic
        MaxDiastolic = rs |> List.maxBy _.Diastolic |> _.Diastolic })

  /// Groups readings by local calendar date and returns one averaged reading per day,
  /// sorted ascending. The returned reading's Timestamp is midnight of that local date.
  /// Comments are dropped (not meaningful for averages).
  let dailyAverages (readings: BloodPressureReading list) : BloodPressureReading list =
    dailyAveragesWithCount readings |> List.map _.Reading

  /// Groups readings by ISO week and returns one averaged reading per week
  /// together with the raw reading count, sorted ascending.
  /// The returned reading's Timestamp is Monday midnight of that week.
  /// Comments are dropped.
  let private weeklyAveragesWithCount (readings: BloodPressureReading list) : AggregatedReading list =
    readings
    |> List.groupBy (fun r ->
      let d = r.Timestamp.ToLocalTime().Date
      ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d))
    |> List.sortBy fst
    |> List.map (fun ((isoYear, week), rs) ->
      let n = List.length rs
      let monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday)
      let offset = TimeZoneInfo.Local.GetUtcOffset(monday)

      { Reading =
          { Id = 0
            MemberId = 0
            Systolic = rs |> List.sumBy _.Systolic |> (fun s -> s / n)
            Diastolic = rs |> List.sumBy _.Diastolic |> (fun s -> s / n)
            HeartRate = rs |> List.sumBy _.HeartRate |> (fun s -> s / n)
            Timestamp = DateTimeOffset(monday, offset)
            Comments = None
            CreatedAt = DateTimeOffset.MinValue
            ModifiedAt = DateTimeOffset.MinValue }
        Count = n
        MinSystolic = rs |> List.minBy _.Systolic |> _.Systolic
        MaxSystolic = rs |> List.maxBy _.Systolic |> _.Systolic
        MinDiastolic = rs |> List.minBy _.Diastolic |> _.Diastolic
        MaxDiastolic = rs |> List.maxBy _.Diastolic |> _.Diastolic })

  /// Groups readings by ISO week and returns one averaged reading per week,
  /// sorted ascending. The returned reading's Timestamp is Monday midnight of that week.
  /// Comments are dropped.
  let weeklyAverages (readings: BloodPressureReading list) : BloodPressureReading list =
    weeklyAveragesWithCount readings |> List.map _.Reading

  /// Groups readings by calendar month and returns one averaged reading per month
  /// together with the raw reading count, sorted ascending.
  /// The returned reading's Timestamp is the 1st of that month midnight.
  /// Comments are dropped.
  let private monthlyAveragesWithCount (readings: BloodPressureReading list) : AggregatedReading list =
    readings
    |> List.groupBy (fun r ->
      let d = r.Timestamp.ToLocalTime().Date
      d.Year, d.Month)
    |> List.sortBy fst
    |> List.map (fun ((year, month), rs) ->
      let n = List.length rs
      let firstOfMonth = DateTime(year, month, 1)
      let offset = TimeZoneInfo.Local.GetUtcOffset(firstOfMonth)

      { Reading =
          { Id = 0
            MemberId = 0
            Systolic = rs |> List.sumBy _.Systolic |> (fun s -> s / n)
            Diastolic = rs |> List.sumBy _.Diastolic |> (fun s -> s / n)
            HeartRate = rs |> List.sumBy _.HeartRate |> (fun s -> s / n)
            Timestamp = DateTimeOffset(firstOfMonth, offset)
            Comments = None
            CreatedAt = DateTimeOffset.MinValue
            ModifiedAt = DateTimeOffset.MinValue }
        Count = n
        MinSystolic = rs |> List.minBy _.Systolic |> _.Systolic
        MaxSystolic = rs |> List.maxBy _.Systolic |> _.Systolic
        MinDiastolic = rs |> List.minBy _.Diastolic |> _.Diastolic
        MaxDiastolic = rs |> List.maxBy _.Diastolic |> _.Diastolic })

  /// Groups readings by calendar month and returns one averaged reading per month,
  /// sorted ascending. The returned reading's Timestamp is the 1st of that month midnight.
  /// Comments are dropped.
  let monthlyAverages (readings: BloodPressureReading list) : BloodPressureReading list =
    monthlyAveragesWithCount readings |> List.map _.Reading

  /// Aggregates readings at the granularity appropriate for the given view:
  /// Weekly → daily averages, Monthly → weekly averages, Yearly → monthly averages.
  /// Returns AggregatedReading so the caller can distinguish single vs. multi-reading periods.
  let aggregate (gran: Granularity) (readings: BloodPressureReading list) : AggregatedReading list =
    match gran with
    | Weekly -> dailyAveragesWithCount readings
    | Monthly -> weeklyAveragesWithCount readings
    | Yearly -> monthlyAveragesWithCount readings

  // ── summary ──────────────────────────────────────────────────────────────────

  /// Summarises readings that have already been filtered to the given period.
  /// Count/min/avg/max are computed over the supplied list; period metadata stamped from the period.
  let summarizeRange (period: TrendPeriod) (rangeReadings: BloodPressureReading list) : WindowSummary =
    match rangeReadings with
    | [] ->
      { Granularity = period.Granularity
        PeriodKey = period.Key
        Label = period.Label
        Count = 0
        MinSystolic = 0
        AvgSystolic = 0
        MaxSystolic = 0
        MinDiastolic = 0
        AvgDiastolic = 0
        MaxDiastolic = 0
        MinHeartRate = 0
        AvgHeartRate = 0
        MaxHeartRate = 0 }
    | rs ->
      let n = List.length rs

      { Granularity = period.Granularity
        PeriodKey = period.Key
        Label = period.Label
        Count = n
        MinSystolic = rs |> List.minBy _.Systolic |> _.Systolic
        AvgSystolic = rs |> List.sumBy _.Systolic |> (fun s -> s / n)
        MaxSystolic = rs |> List.maxBy _.Systolic |> _.Systolic
        MinDiastolic = rs |> List.minBy _.Diastolic |> _.Diastolic
        AvgDiastolic = rs |> List.sumBy _.Diastolic |> (fun s -> s / n)
        MaxDiastolic = rs |> List.maxBy _.Diastolic |> _.Diastolic
        MinHeartRate = rs |> List.minBy _.HeartRate |> _.HeartRate
        AvgHeartRate = rs |> List.sumBy _.HeartRate |> (fun s -> s / n)
        MaxHeartRate = rs |> List.maxBy _.HeartRate |> _.HeartRate }
