namespace BpMonitor.Core

type WindowSummary =
  { Days: int
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

  /// Returns readings whose Timestamp is >= (now - days).
  let since (now: DateTimeOffset) (days: int) (readings: BloodPressureReading list) : BloodPressureReading list =
    let cutoff = now.AddDays(float -days)
    readings |> List.filter (fun r -> r.Timestamp >= cutoff)

  /// Groups readings by local calendar date and returns one averaged reading per day,
  /// sorted ascending. The returned reading's Timestamp is midnight of that local date.
  /// Comments are dropped (not meaningful for averages).
  let dailyAverages (readings: BloodPressureReading list) : BloodPressureReading list =
    readings
    |> List.groupBy (fun r -> r.Timestamp.ToLocalTime().Date)
    |> List.sortBy fst
    |> List.map (fun (date, rs) ->
      let n = List.length rs
      let offset = TimeZoneInfo.Local.GetUtcOffset(date)

      { Id = 0
        MemberId = 0
        Systolic = rs |> List.sumBy _.Systolic |> (fun s -> s / n)
        Diastolic = rs |> List.sumBy _.Diastolic |> (fun s -> s / n)
        HeartRate = rs |> List.sumBy _.HeartRate |> (fun s -> s / n)
        Timestamp = DateTimeOffset(date, offset)
        Comments = None
        CreatedAt = DateTimeOffset.MinValue
        ModifiedAt = DateTimeOffset.MinValue })

  /// Summarizes readings in the given window (days before now).
  /// Averages are truncated to int. Count = 0 when no readings exist in the window.
  let summarize (now: DateTimeOffset) (days: int) (readings: BloodPressureReading list) : WindowSummary =
    let window = since now days readings

    match window with
    | [] ->
      { Days = days
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

      { Days = days
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
