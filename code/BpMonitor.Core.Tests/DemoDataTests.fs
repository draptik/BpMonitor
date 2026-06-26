module DemoDataTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

let private ranges = ReadingRanges.defaults
let private now = DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero)

// ── member shape ─────────────────────────────────────────────────────────────

[<Fact>]
let ``simpsons produces exactly 6 member entries`` () =
  let result = DemoData.simpsons ranges now
  test <@ result.Length = 6 @>

[<Fact>]
let ``simpsons has exactly one admin member`` () =
  let result = DemoData.simpsons ranges now
  let admins = result |> List.filter (fun (spec, _) -> spec.IsAdmin)
  test <@ admins.Length = 1 @>

[<Fact>]
let ``simpsons admin is Marge Simpson`` () =
  let result = DemoData.simpsons ranges now
  let spec, _ = result |> List.find (fun (spec, _) -> spec.IsAdmin)
  test <@ spec.Name = "Marge Simpson" @>

// ── reading validity ──────────────────────────────────────────────────────────

[<Fact>]
let ``all generated readings are in range (no parse errors)`` () =
  let result = DemoData.simpsons ranges now

  for _, readings in result do
    for r in readings do
      test
        <@
          r.Systolic >= ranges.SystolicMin
          && r.Systolic <= ranges.SystolicMax
          && r.Diastolic >= ranges.DiastolicMin
          && r.Diastolic <= ranges.DiastolicMax
          && r.HeartRate >= ranges.HeartRateMin
          && r.HeartRate <= ranges.HeartRateMax
        @>

// ── timestamp span ────────────────────────────────────────────────────────────

[<Fact>]
let ``readings span roughly 5 years`` () =
  let result = DemoData.simpsons ranges now
  let allReadings = result |> List.collect snd
  let earliest = allReadings |> List.map _.Timestamp |> List.min
  let latest = allReadings |> List.map _.Timestamp |> List.max
  let spanDays = (latest - earliest).TotalDays
  // Expect at least 4 years of spread across all members
  test <@ spanDays >= 4.0 * 365.0 @>

[<Fact>]
let ``readings end at or before the anchor time`` () =
  let result = DemoData.simpsons ranges now
  let allReadings = result |> List.collect snd
  let latest = allReadings |> List.map _.Timestamp |> List.max
  test <@ latest <= now @>

// ── determinism ──────────────────────────────────────────────────────────────

[<Fact>]
let ``same anchor produces identical results (deterministic)`` () =
  let r1 = DemoData.simpsons ranges now
  let r2 = DemoData.simpsons ranges now
  let counts1 = r1 |> List.map (fun (s, rs) -> s.Name, rs.Length)
  let counts2 = r2 |> List.map (fun (s, rs) -> s.Name, rs.Length)
  test <@ counts1 = counts2 @>

// ── member profiles are distinct ──────────────────────────────────────────────

let private meanSys (result: (MemberSpec * BloodPressureReading list) list) name =
  let _, readings = result |> List.find (fun (s, _) -> s.Name = name)
  readings |> List.averageBy (fun r -> float r.Systolic)

[<Fact>]
let ``Homer mean systolic is higher than Marge mean systolic`` () =
  let result = DemoData.simpsons ranges now
  test <@ meanSys result "Homer Simpson" > meanSys result "Marge Simpson" @>

[<Fact>]
let ``Marge mean systolic is higher than Bart mean systolic`` () =
  let result = DemoData.simpsons ranges now
  test <@ meanSys result "Marge Simpson" > meanSys result "Bart Simpson" @>

// ── Ned Flanders: a Fig. 5-style narrative (Wegier et al. 2021) ───────────────
// Elevated BP, a multi-day gap in home monitoring, then visibly improved control
// after starting treatment — exercises the /recent chart's missing-data dashing
// and LOWESS trend line on the same kind of story the paper's mock-up display shows.

let private nedReadings () =
  DemoData.simpsons ranges now
  |> List.find (fun (s, _) -> s.Name = "Ned Flanders")
  |> snd

[<Fact>]
let ``Ned Flanders has a multi-day gap in his most recent readings`` () =
  let readings = nedReadings ()
  let sorted = readings |> List.sortBy _.Timestamp

  let largestGapDays =
    sorted
    |> List.pairwise
    |> List.map (fun (a, b) -> (b.Timestamp.ToLocalTime().Date - a.Timestamp.ToLocalTime().Date).Days)
    |> List.max

  // > 3 missing days exceeds 10% of a 30-day window, triggering the dashed-line treatment.
  test <@ largestGapDays - 1 > 3 @>

[<Fact>]
let ``Ned Flanders' blood pressure improves after the gap`` () =
  let readings = nedReadings ()
  let sorted = readings |> List.sortBy _.Timestamp

  let gapEndIdx =
    sorted
    |> List.pairwise
    |> List.indexed
    |> List.maxBy (fun (_, (a, b)) -> (b.Timestamp.ToLocalTime().Date - a.Timestamp.ToLocalTime().Date).Days)
    |> fun (i, _) -> i + 1

  let before = sorted |> List.take gapEndIdx
  let after = sorted |> List.skip gapEndIdx

  test
    <@ (before |> List.averageBy (fun r -> float r.Systolic)) > (after |> List.averageBy (fun r -> float r.Systolic)) @>

  test
    <@ (before |> List.averageBy (fun r -> float r.Diastolic)) > (after |> List.averageBy (fun r -> float r.Diastolic)) @>

[<Fact>]
let ``Ned Flanders' gap falls within the last 30 days, so it's visible on the /recent chart`` () =
  let readings = nedReadings ()

  let last30Days =
    readings |> List.filter (fun r -> r.Timestamp >= now.AddDays(-30.0))

  let largestGapWithinWindow =
    last30Days
    |> List.sortBy _.Timestamp
    |> List.pairwise
    |> List.map (fun (a, b) -> (b.Timestamp.ToLocalTime().Date - a.Timestamp.ToLocalTime().Date).Days)
    |> List.max

  test <@ largestGapWithinWindow - 1 > 3 @>

[<Fact>]
let ``Ned Flanders' last 30 days mirror Fig. 5's systolic/diastolic values exactly, in order`` () =
  // Transcribed verbatim from the "Blood Pressure Values" data table in Fig. 5
  // (Wegier et al. 2021, docs/resources/12911_2021_Article_1598.pdf, page 10).
  let expectedSystolic =
    [ 147
      157
      155
      160
      154
      161
      158
      150
      141
      163
      144
      149
      158
      181
      169
      173
      153
      154
      151
      163
      157
      155
      146
      167
      155
      164
      136
      129
      125
      126
      134
      128
      130
      123
      126
      132
      119
      149
      129
      141
      117
      140
      151
      120
      122
      150
      128
      132
      129
      125
      132
      138
      140
      135
      122
      123
      116 ]

  let expectedDiastolic =
    [ 100
      101
      105
      122
      93
      99
      110
      100
      92
      106
      96
      98
      101
      98
      102
      107
      114
      100
      96
      119
      94
      93
      93
      101
      96
      114
      76
      76
      69
      73
      79
      77
      72
      76
      72
      80
      77
      85
      85
      78
      73
      80
      79
      70
      70
      53
      81
      73
      75
      77
      77
      79
      76
      80
      82
      76
      70 ]

  let readings = nedReadings ()

  let last30Days =
    readings
    |> List.filter (fun r -> r.Timestamp >= now.AddDays(-30.0))
    |> List.sortBy _.Timestamp

  test <@ (last30Days |> List.map _.Systolic) = expectedSystolic @>
  test <@ (last30Days |> List.map _.Diastolic) = expectedDiastolic @>

[<Fact>]
let ``Ned Flanders' Fig. 5 readings are uniformly spaced apart from the one gap`` () =
  // LOWESS picks its neighborhood by point rank, not a fixed time window — if the
  // 57 Fig. 5 readings were unevenly dense (e.g., sparse before the gap, packed
  // afterward). The fit would be skewed by whichever region's points dominate
  // the rank-nearest search, distorting the curve's shape relative to the paper's.
  // Spacing must be uniform throughout except for the one intentional gap.
  let readings = nedReadings ()

  let last30Days =
    readings
    |> List.filter (fun r -> r.Timestamp >= now.AddDays(-30.0))
    |> List.sortBy _.Timestamp
    |> List.map _.Timestamp

  let gapsInDays =
    last30Days |> List.pairwise |> List.map (fun (a, b) -> (b - a).TotalDays)

  let ordinarySteps = gapsInDays |> List.filter (fun d -> d < 1.0)
  let bigGaps = gapsInDays |> List.filter (fun d -> d >= 1.0)

  // Exactly one big gap (the cuff-broken stretch); every other step is the same
  // uniform fractional-day spacing (within floating-point tolerance).
  test <@ bigGaps.Length = 1 @>
  test <@ ordinarySteps.Length = gapsInDays.Length - 1 @>
  let minStep, maxStep = List.min ordinarySteps, List.max ordinarySteps
  test <@ maxStep - minStep < 0.001 @>

[<Fact>]
let ``each member has enough readings for trend granularities`` () =
  let result = DemoData.simpsons ranges now
  // Minimum: expect at least 52 readings per member (roughly 1 per week for a year)
  for _, readings in result do
    test <@ readings.Length >= 52 @>
