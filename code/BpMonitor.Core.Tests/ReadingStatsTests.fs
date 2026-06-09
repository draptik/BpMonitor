module ReadingStatsTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

let private mkReading id sys dia hr (ts: DateTimeOffset) =
  { Id = id
    MemberId = 1
    Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = ts
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private now = DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero)

// ── since ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``since: reading exactly at cutoff is included`` () =
  let cutoff = now.AddDays(-7)
  let r = mkReading 1 120 80 70 cutoff
  test <@ ReadingStats.since now 7 [ r ] = [ r ] @>

[<Fact>]
let ``since: reading one second before cutoff is excluded`` () =
  let justBefore = now.AddDays(-7).AddSeconds(-1.0)
  let r = mkReading 1 120 80 70 justBefore
  test <@ ReadingStats.since now 7 [ r ] = [] @>

[<Fact>]
let ``since: reading after now is included`` () =
  let future = now.AddDays(1)
  let r = mkReading 1 120 80 70 future
  test <@ ReadingStats.since now 7 [ r ] = [ r ] @>

[<Fact>]
let ``since: returns only readings within window from a mixed list`` () =
  let inWindow = mkReading 1 120 80 70 (now.AddDays(-3))
  let atEdge = mkReading 2 130 85 72 (now.AddDays(-30))
  let outside = mkReading 3 140 90 75 (now.AddDays(-31))
  let result = ReadingStats.since now 30 [ inWindow; atEdge; outside ]
  test <@ result |> List.contains inWindow @>
  test <@ result |> List.contains atEdge @>
  test <@ result |> List.contains outside |> not @>

[<Fact>]
let ``since: empty list returns empty`` () =
  test <@ ReadingStats.since now 7 [] = [] @>

// ── dailyAverages ──────────────────────────────────────────────────────────

[<Fact>]
let ``dailyAverages: empty list returns empty`` () =
  test <@ ReadingStats.dailyAverages [] = [] @>

[<Fact>]
let ``dailyAverages: single reading returns one entry with same values`` () =
  let r = mkReading 1 130 85 68 (now.AddDays(-1))
  let result = ReadingStats.dailyAverages [ r ]
  test <@ result.Length = 1 @>
  let day = result.[0]
  test <@ day.Systolic = 130 @>
  test <@ day.Diastolic = 85 @>
  test <@ day.HeartRate = 68 @>

[<Fact>]
let ``dailyAverages: two readings on same day are averaged`` () =
  let day0 = now.Date
  let r1 = mkReading 1 120 80 60 (DateTimeOffset(day0.AddHours(8), TimeSpan.Zero))
  let r2 = mkReading 2 140 90 80 (DateTimeOffset(day0.AddHours(20), TimeSpan.Zero))
  let result = ReadingStats.dailyAverages [ r1; r2 ]
  test <@ result.Length = 1 @>
  test <@ result.[0].Systolic = 130 @>
  test <@ result.[0].Diastolic = 85 @>
  test <@ result.[0].HeartRate = 70 @>

[<Fact>]
let ``dailyAverages: readings on different days produce one entry per day`` () =
  let r1 = mkReading 1 120 80 60 (now.AddDays(-2))
  let r2 = mkReading 2 130 85 70 (now.AddDays(-1))
  let result = ReadingStats.dailyAverages [ r1; r2 ]
  test <@ result.Length = 2 @>

[<Fact>]
let ``dailyAverages: result is sorted ascending by date`` () =
  let r1 = mkReading 1 130 85 70 (now.AddDays(-1))
  let r2 = mkReading 2 120 80 60 (now.AddDays(-3))
  let result = ReadingStats.dailyAverages [ r1; r2 ]
  test <@ result.[0].Timestamp < result.[1].Timestamp @>

[<Fact>]
let ``dailyAverages: timestamp is midnight of the local date`` () =
  let r = mkReading 1 120 80 60 (now.AddDays(-1))
  let day = ReadingStats.dailyAverages [ r ] |> List.exactlyOne
  let ts = day.Timestamp.ToLocalTime()
  let h, m, s = ts.Hour, ts.Minute, ts.Second
  test <@ h = 0 && m = 0 && s = 0 @>

// ── summarize ──────────────────────────────────────────────────────────────

[<Fact>]
let ``summarize: empty list yields Count=0`` () =
  let s = ReadingStats.summarize now 7 []
  test <@ s.Days = 7 @>
  test <@ s.Count = 0 @>

[<Fact>]
let ``summarize: single reading yields correct averages`` () =
  let r = mkReading 1 130 85 68 (now.AddDays(-1))
  let s = ReadingStats.summarize now 7 [ r ]
  test <@ s.Count = 1 @>
  test <@ s.AvgSystolic = 130 @>
  test <@ s.AvgDiastolic = 85 @>
  test <@ s.AvgHeartRate = 68 @>

[<Fact>]
let ``summarize: averages are truncated to int`` () =
  let r1 = mkReading 1 120 80 70 (now.AddDays(-1))
  let r2 = mkReading 2 121 81 71 (now.AddDays(-2))
  let s = ReadingStats.summarize now 7 [ r1; r2 ]
  test <@ s.Count = 2 @>
  test <@ s.AvgSystolic = 120 @>
  test <@ s.AvgDiastolic = 80 @>
  test <@ s.AvgHeartRate = 70 @>

[<Fact>]
let ``summarize: only counts readings within the window`` () =
  let inWindow = mkReading 1 120 80 70 (now.AddDays(-3))
  let outside = mkReading 2 180 110 90 (now.AddDays(-100))
  let s = ReadingStats.summarize now 7 [ inWindow; outside ]
  test <@ s.Count = 1 @>
  test <@ s.AvgSystolic = 120 @>

[<Fact>]
let ``summarize: Days field reflects requested window`` () =
  let s = ReadingStats.summarize now 30 []
  test <@ s.Days = 30 @>

[<Fact>]
let ``summarize: min and max are correct for multiple readings`` () =
  let r1 = mkReading 1 110 75 60 (now.AddDays(-1))
  let r2 = mkReading 2 130 85 70 (now.AddDays(-2))
  let r3 = mkReading 3 150 95 80 (now.AddDays(-3))
  let s = ReadingStats.summarize now 7 [ r1; r2; r3 ]
  test <@ s.MinSystolic = 110 @>
  test <@ s.MaxSystolic = 150 @>
  test <@ s.MinDiastolic = 75 @>
  test <@ s.MaxDiastolic = 95 @>
  test <@ s.MinHeartRate = 60 @>
  test <@ s.MaxHeartRate = 80 @>

[<Fact>]
let ``summarize: min and max equal avg for a single reading`` () =
  let r = mkReading 1 130 85 68 (now.AddDays(-1))
  let s = ReadingStats.summarize now 7 [ r ]
  test <@ s.MinSystolic = 130 && s.MaxSystolic = 130 @>
  test <@ s.MinDiastolic = 85 && s.MaxDiastolic = 85 @>
  test <@ s.MinHeartRate = 68 && s.MaxHeartRate = 68 @>

[<Fact>]
let ``summarize: min and max are 0 when window is empty`` () =
  let s = ReadingStats.summarize now 7 []
  test <@ s.MinSystolic = 0 && s.MaxSystolic = 0 @>
  test <@ s.MinDiastolic = 0 && s.MaxDiastolic = 0 @>
  test <@ s.MinHeartRate = 0 && s.MaxHeartRate = 0 @>
