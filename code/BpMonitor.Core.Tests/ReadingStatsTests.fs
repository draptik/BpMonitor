module ReadingStatsTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open TestBuilders

let private now = DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero)

// Helper: build the "current weekly" period for `now`
let private currentWeeklyPeriod = TrendPeriod.current Weekly now

// ── between ───────────────────────────────────────────────────────────────────

[<Fact>]
let ``between: reading exactly at start is included`` () =
  let start = now.AddDays(-7.0)
  let r = mkReading 1 1 120 80 70 start
  test <@ ReadingStats.between start (now.AddDays(1.0)) [ r ] = [ r ] @>

[<Fact>]
let ``between: reading exactly at endExcl is excluded`` () =
  let endExcl = now
  let r = mkReading 1 1 120 80 70 endExcl
  test <@ ReadingStats.between (now.AddDays(-7.0)) endExcl [ r ] = [] @>

[<Fact>]
let ``between: reading inside range is included`` () =
  let r = mkReading 1 1 120 80 70 (now.AddDays(-3.0))
  test <@ ReadingStats.between (now.AddDays(-7.0)) now [ r ] = [ r ] @>

[<Fact>]
let ``between: reading before range is excluded`` () =
  let r = mkReading 1 1 120 80 70 (now.AddDays(-10.0))
  test <@ ReadingStats.between (now.AddDays(-7.0)) now [ r ] = [] @>

[<Fact>]
let ``between: empty list returns empty`` () =
  test <@ ReadingStats.between now (now.AddDays(1.0)) [] = [] @>

// ── dailyAverages ──────────────────────────────────────────────────────────────

[<Fact>]
let ``dailyAverages: empty list returns empty`` () =
  test <@ ReadingStats.dailyAverages [] = [] @>

[<Fact>]
let ``dailyAverages: single reading returns one entry with same values`` () =
  let r = mkReading 1 1 130 85 68 (now.AddDays(-1.0))
  let result = ReadingStats.dailyAverages [ r ]
  test <@ result.Length = 1 @>
  let day = result[0]
  test <@ day.Systolic = 130 @>
  test <@ day.Diastolic = 85 @>
  test <@ day.HeartRate = 68 @>

[<Fact>]
let ``dailyAverages: two readings on same day are averaged`` () =
  let day0 = now.Date
  let r1 = mkReading 1 1 120 80 60 (DateTimeOffset(day0.AddHours(8.0), TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(day0.AddHours(20.0), TimeSpan.Zero))

  let result = ReadingStats.dailyAverages [ r1; r2 ]
  test <@ result.Length = 1 @>
  test <@ result[0].Systolic = 130 @>
  test <@ result[0].Diastolic = 85 @>
  test <@ result[0].HeartRate = 70 @>

[<Fact>]
let ``dailyAverages: readings on different days produce one entry per day`` () =
  let r1 = mkReading 1 1 120 80 60 (now.AddDays(-2.0))
  let r2 = mkReading 2 1 130 85 70 (now.AddDays(-1.0))
  let result = ReadingStats.dailyAverages [ r1; r2 ]
  test <@ result.Length = 2 @>

[<Fact>]
let ``dailyAverages: result is sorted ascending by date`` () =
  let r1 = mkReading 1 1 130 85 70 (now.AddDays(-1.0))
  let r2 = mkReading 2 1 120 80 60 (now.AddDays(-3.0))
  let result = ReadingStats.dailyAverages [ r1; r2 ]
  test <@ result[0].Timestamp < result[1].Timestamp @>

[<Fact>]
let ``dailyAverages: timestamp is midnight of the local date`` () =
  let r = mkReading 1 1 120 80 60 (now.AddDays(-1.0))
  let day = ReadingStats.dailyAverages [ r ] |> List.exactlyOne
  let ts = day.Timestamp.ToLocalTime()
  let h, m, s = ts.Hour, ts.Minute, ts.Second
  test <@ h = 0 && m = 0 && s = 0 @>

// ── weeklyAverages ────────────────────────────────────────────────────────────

[<Fact>]
let ``weeklyAverages: empty list returns empty`` () =
  test <@ ReadingStats.weeklyAverages [] = [] @>

[<Fact>]
let ``weeklyAverages: readings in same ISO week are averaged into one entry`` () =
  // 2026-W24: Mon 2026-06-08 ... Sun 2026-06-14
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.weeklyAverages [ r1; r2 ]
  test <@ result.Length = 1 @>
  test <@ result[0].Systolic = 130 @>
  test <@ result[0].Diastolic = 85 @>
  test <@ result[0].HeartRate = 70 @>

[<Fact>]
let ``weeklyAverages: readings in different weeks produce one entry per week`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero)) // W23

  let r2 =
    mkReading 2 1 130 85 70 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero)) // W24

  let result = ReadingStats.weeklyAverages [ r1; r2 ]
  test <@ result.Length = 2 @>

[<Fact>]
let ``weeklyAverages: result is sorted ascending`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero)) // W24

  let r2 =
    mkReading 2 1 130 85 70 (DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero)) // W23

  let result = ReadingStats.weeklyAverages [ r1; r2 ]
  test <@ result[0].Timestamp < result[1].Timestamp @>

[<Fact>]
let ``weeklyAverages: timestamp is Monday midnight of the ISO week`` () =
  let r =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 10, 15, 0, 0, TimeSpan.Zero)) // W24 (Wednesday)

  let result = ReadingStats.weeklyAverages [ r ]
  let ts = result[0].Timestamp.ToLocalTime()
  let y, mo, d = ts.Year, ts.Month, ts.Day
  // 2026-W24 starts on 2026-06-08 (Monday)
  test <@ y = 2026 && mo = 6 && d = 8 @>

// ── monthlyAverages ───────────────────────────────────────────────────────────

[<Fact>]
let ``monthlyAverages: empty list returns empty`` () =
  test <@ ReadingStats.monthlyAverages [] = [] @>

[<Fact>]
let ``monthlyAverages: readings in same month are averaged into one entry`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 5, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(2026, 6, 20, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.monthlyAverages [ r1; r2 ]
  test <@ result.Length = 1 @>
  test <@ result[0].Systolic = 130 @>

[<Fact>]
let ``monthlyAverages: readings in different months produce one entry per month`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 130 85 70 (DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.monthlyAverages [ r1; r2 ]
  test <@ result.Length = 2 @>

[<Fact>]
let ``monthlyAverages: result is sorted ascending`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 130 85 70 (DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.monthlyAverages [ r1; r2 ]
  test <@ result[0].Timestamp < result[1].Timestamp @>

[<Fact>]
let ``monthlyAverages: timestamp is first of month midnight`` () =
  let r =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.monthlyAverages [ r ]
  let ts = result[0].Timestamp.ToLocalTime()
  let y, mo, d = ts.Year, ts.Month, ts.Day
  test <@ y = 2026 && mo = 6 && d = 1 @>

// ── aggregate ─────────────────────────────────────────────────────────────────

[<Fact>]
let ``aggregate Weekly delegates to dailyAverages`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(2026, 6, 8, 20, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.aggregate Weekly [ r1; r2 ]
  // Two readings on same day → one daily average
  test <@ result.Length = 1 @>

[<Fact>]
let ``aggregate Monthly delegates to weeklyAverages`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero)) // W23

  let r2 =
    mkReading 2 1 130 85 70 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero)) // W24

  let result = ReadingStats.aggregate Monthly [ r1; r2 ]
  // Two different weeks → two weekly averages
  test <@ result.Length = 2 @>

[<Fact>]
let ``aggregate Yearly delegates to monthlyAverages`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 130 85 70 (DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.aggregate Yearly [ r1; r2 ]
  // Two different months → two monthly averages
  test <@ result.Length = 2 @>

[<Fact>]
let ``aggregate Weekly: two readings on same day yields Count=2`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(2026, 6, 8, 20, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.aggregate Weekly [ r1; r2 ]
  test <@ result[0].Count = 2 @>

[<Fact>]
let ``aggregate Weekly: single reading on a day yields Count=1`` () =
  let r = mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))
  let result = ReadingStats.aggregate Weekly [ r ]
  test <@ result[0].Count = 1 @>

[<Fact>]
let ``aggregate Monthly: two readings in same ISO week yields Count=2`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(2026, 6, 9, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.aggregate Monthly [ r1; r2 ]
  test <@ result[0].Count = 2 @>

[<Fact>]
let ``aggregate Yearly: two readings in same calendar month yields Count=2`` () =
  let r1 =
    mkReading 1 1 120 80 60 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))

  let r2 =
    mkReading 2 1 140 90 80 (DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero))

  let result = ReadingStats.aggregate Yearly [ r1; r2 ]
  test <@ result[0].Count = 2 @>

// ── aggregate: min/max fields ─────────────────────────────────────────────────

[<Fact>]
let ``aggregate Weekly: multi-reading period carries correct MinSystolic and MaxSystolic`` () =
  let day = DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero)
  let r1 = mkReading 1 1 110 75 60 (day.AddHours(8.0))
  let r2 = mkReading 2 1 130 85 70 (day.AddHours(12.0))
  let r3 = mkReading 3 1 150 95 80 (day.AddHours(20.0))
  let a = ReadingStats.aggregate Weekly [ r1; r2; r3 ] |> List.exactlyOne
  test <@ a.MinSystolic = 110 @>
  test <@ a.MaxSystolic = 150 @>

[<Fact>]
let ``aggregate Weekly: multi-reading period carries correct MinDiastolic and MaxDiastolic`` () =
  let day = DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero)
  let r1 = mkReading 1 1 110 75 60 (day.AddHours(8.0))
  let r2 = mkReading 2 1 130 85 70 (day.AddHours(12.0))
  let r3 = mkReading 3 1 150 95 80 (day.AddHours(20.0))
  let a = ReadingStats.aggregate Weekly [ r1; r2; r3 ] |> List.exactlyOne
  test <@ a.MinDiastolic = 75 @>
  test <@ a.MaxDiastolic = 95 @>

[<Fact>]
let ``aggregate Weekly: single-reading period has MinSystolic = MaxSystolic = Reading.Systolic`` () =
  let r = mkReading 1 1 130 85 70 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))
  let a = ReadingStats.aggregate Weekly [ r ] |> List.exactlyOne
  test <@ a.MinSystolic = a.Reading.Systolic @>
  test <@ a.MaxSystolic = a.Reading.Systolic @>

[<Fact>]
let ``aggregate Weekly: single-reading period has MinDiastolic = MaxDiastolic = Reading.Diastolic`` () =
  let r = mkReading 1 1 130 85 70 (DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero))
  let a = ReadingStats.aggregate Weekly [ r ] |> List.exactlyOne
  test <@ a.MinDiastolic = a.Reading.Diastolic @>
  test <@ a.MaxDiastolic = a.Reading.Diastolic @>

// ── summarizeRange ────────────────────────────────────────────────────────────

[<Fact>]
let ``summarizeRange: empty list yields Count=0`` () =
  let s = ReadingStats.summarizeRange currentWeeklyPeriod []
  test <@ s.Count = 0 @>

[<Fact>]
let ``summarizeRange: period metadata is stamped on the result`` () =
  let s = ReadingStats.summarizeRange currentWeeklyPeriod []
  let gran = s.Granularity
  let key = s.PeriodKey
  let label = s.Label
  test <@ gran = Weekly @>
  test <@ key = currentWeeklyPeriod.Key @>
  test <@ label = "This Week" @>

[<Fact>]
let ``summarizeRange: single reading yields correct averages`` () =
  let r = mkReading 1 1 130 85 68 (now.AddDays(-1.0))
  let s = ReadingStats.summarizeRange currentWeeklyPeriod [ r ]
  test <@ s.Count = 1 @>
  test <@ s.AvgSystolic = 130 @>
  test <@ s.AvgDiastolic = 85 @>
  test <@ s.AvgHeartRate = 68 @>

[<Fact>]
let ``summarizeRange: averages are truncated to int`` () =
  let r1 = mkReading 1 1 120 80 70 (now.AddDays(-1.0))
  let r2 = mkReading 2 1 121 81 71 (now.AddDays(-2.0))
  let s = ReadingStats.summarizeRange currentWeeklyPeriod [ r1; r2 ]
  test <@ s.Count = 2 @>
  test <@ s.AvgSystolic = 120 @>
  test <@ s.AvgDiastolic = 80 @>
  test <@ s.AvgHeartRate = 70 @>

[<Fact>]
let ``summarizeRange: min and max are correct for multiple readings`` () =
  let r1 = mkReading 1 1 110 75 60 (now.AddDays(-1.0))
  let r2 = mkReading 2 1 130 85 70 (now.AddDays(-2.0))
  let r3 = mkReading 3 1 150 95 80 (now.AddDays(-3.0))
  let s = ReadingStats.summarizeRange currentWeeklyPeriod [ r1; r2; r3 ]
  test <@ s.MinSystolic = 110 @>
  test <@ s.MaxSystolic = 150 @>
  test <@ s.MinDiastolic = 75 @>
  test <@ s.MaxDiastolic = 95 @>
  test <@ s.MinHeartRate = 60 @>
  test <@ s.MaxHeartRate = 80 @>

[<Fact>]
let ``summarizeRange: min and max equal avg for a single reading`` () =
  let r = mkReading 1 1 130 85 68 (now.AddDays(-1.0))
  let s = ReadingStats.summarizeRange currentWeeklyPeriod [ r ]
  test <@ s.MinSystolic = 130 && s.MaxSystolic = 130 @>
  test <@ s.MinDiastolic = 85 && s.MaxDiastolic = 85 @>
  test <@ s.MinHeartRate = 68 && s.MaxHeartRate = 68 @>

[<Fact>]
let ``summarizeRange: min and max are 0 when window is empty`` () =
  let s = ReadingStats.summarizeRange currentWeeklyPeriod []
  test <@ s.MinSystolic = 0 && s.MaxSystolic = 0 @>
  test <@ s.MinDiastolic = 0 && s.MaxDiastolic = 0 @>
  test <@ s.MinHeartRate = 0 && s.MaxHeartRate = 0 @>
