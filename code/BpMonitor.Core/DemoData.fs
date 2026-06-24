namespace BpMonitor.Core

open System

/// A lightweight descriptor for a family member used by the demo-data generator.
/// The seeder (BpMonitor.Data.DemoSeeder) converts these into real FamilyMember records.
type MemberSpec = { Name: string; IsAdmin: bool }

/// Deterministic demo-data generator — the Simpson family.
///
/// All generations are pure (no I/O). A per-member fixed Random seed ensures
/// identical output for the same `now` value across multiple calls.
module DemoData =

  // ── internal profile definition ──────────────────────────────────────────

  [<NoComparison; NoEquality>]
  type private Profile =
    {
      Spec: MemberSpec
      SysBase: int
      SysJitter: int
      DiaBase: int
      DiaJitter: int
      HrBase: int
      HrJitter: int
      /// Average readings emitted per week.
      ReadingsPerWeek: float
      /// Rotating comment pool; None entries produce no comment.
      Comments: string option list
      /// Fixed seed so generation is reproducible.
      RandomSeed: int
    }

  let private profiles =
    [ { Spec =
          { Name = "Marge Simpson"
            IsAdmin = true }
        SysBase = 118
        SysJitter = 6
        DiaBase = 76
        DiaJitter = 4
        HrBase = 68
        HrJitter = 5
        ReadingsPerWeek = 3.5
        Comments = [ None; Some "morning"; Some "after yoga"; None; Some "calm day"; None ]
        RandomSeed = 1 }
      { Spec =
          { Name = "Homer Simpson"
            IsAdmin = false }
        SysBase = 152
        SysJitter = 12
        DiaBase = 96
        DiaJitter = 8
        HrBase = 82
        HrJitter = 8
        ReadingsPerWeek = 5.0
        Comments =
          [ None
            Some "after donuts"
            Some "Duff o'clock"
            None
            Some "stressful day at work"
            Some "mmm beer"
            None
            None ]
        RandomSeed = 2 }
      { Spec =
          { Name = "Bart Simpson"
            IsAdmin = false }
        SysBase = 108
        SysJitter = 5
        DiaBase = 68
        DiaJitter = 4
        HrBase = 74
        HrJitter = 6
        ReadingsPerWeek = 1.5
        Comments = [ None; None; Some "after skateboarding"; None ]
        RandomSeed = 3 }
      { Spec =
          { Name = "Lisa Simpson"
            IsAdmin = false }
        SysBase = 110
        SysJitter = 4
        DiaBase = 70
        DiaJitter = 3
        HrBase = 67
        HrJitter = 5
        ReadingsPerWeek = 1.5
        Comments = [ None; Some "before recital"; None; Some "morning meditation"; None ]
        RandomSeed = 4 }
      { Spec =
          { Name = "Abe Simpson"
            IsAdmin = false }
        SysBase = 145
        SysJitter = 14
        DiaBase = 88
        DiaJitter = 9
        HrBase = 63
        HrJitter = 7
        ReadingsPerWeek = 2.0
        Comments = [ None; None; Some "after nap"; Some "feeling dizzy"; None; None ]
        RandomSeed = 5 } ]

  // ── helpers ──────────────────────────────────────────────────────────────

  let private clamp lo hi v = max lo (min hi v)

  // Clamps raw vitals into range and parses them into a reading. Shared by every
  // generator below (the random-jitter Simpson profiles and Ned Flanders' scripted
  // narrative alike), so there's one place that builds an `unvalidated` record.
  let private buildReading
    (ranges: ReadingRanges)
    (systolic: int)
    (diastolic: int)
    (heartRate: int)
    (timestamp: DateTimeOffset)
    (comment: string option)
    : BloodPressureReading option =
    let unvalidated =
      { Systolic = clamp ranges.SystolicMin ranges.SystolicMax systolic
        Diastolic = clamp ranges.DiastolicMin ranges.DiastolicMax diastolic
        HeartRate = clamp ranges.HeartRateMin ranges.HeartRateMax heartRate
        Timestamp = timestamp
        Comments = comment }

    match BloodPressureReading.parse ranges unvalidated with
    | Ok r -> Some r
    | Error _ -> None // never reached: values are clamped into range

  // ── reading generation ────────────────────────────────────────────────────

  let private generateReadings (ranges: ReadingRanges) (now: DateTimeOffset) (profile: Profile) =
    let rng = Random(profile.RandomSeed)
    let spanDays = int (365.25 * 5.0) // ≈ 1826 days
    let startDate = now.AddDays(float -spanDays)
    let probPerDay = profile.ReadingsPerWeek / 7.0

    [ 0 .. spanDays - 1 ]
    |> List.choose (fun dayOffset ->
      if rng.NextDouble() >= probPerDay then
        None
      else
        let date = startDate.AddDays(float dayOffset)

        // Guard: skip if the date/time arithmetic would exceed now (edge of window).
        if date > now then
          None
        else
          let hour = rng.Next(6, 21)
          let minute = rng.Next(0, 60)

          // Use UTC so readings are timezone-neutral.
          let ts =
            DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.Zero)

          // Random draw order matters for determinism — preserve sys, dia, hr, then
          // comment, matching the original sequence so existing demo values don't shift.
          let sys = profile.SysBase + rng.Next(-profile.SysJitter, profile.SysJitter + 1)
          let dia = profile.DiaBase + rng.Next(-profile.DiaJitter, profile.DiaJitter + 1)
          let hr = profile.HrBase + rng.Next(-profile.HrJitter, profile.HrJitter + 1)
          let commentIdx = rng.Next(0, profile.Comments.Length)
          let comment = profile.Comments[commentIdx]

          buildReading ranges sys dia hr ts comment)

  // ── Ned Flanders: a Fig. 5-style narrative (Wegier et al. 2021) ───────────
  //
  // Unlike the random-jitter profiles above, Ned's last 30 days mirror the
  // "Blood Pressure Values" data table from Fig. 5 exactly, verbatim, in order:
  // 9 elevated readings, a 5-day gap (4 missing days — he forgot his cuff on a
  // mission trip, matching the figure's "BP cuff broken for four days"
  // annotation), then 48 readings that visibly improve after starting
  // medication. All 57 readings are spaced uniformly (except for that one gap),
  // so the density doesn't skew the LOWESS fit — see the comment at
  // `generateNedReadings`. A year of sparser, mildly elevated background
  // readings precedes it for /trends and /history.

  let private nedSpec =
    { Name = "Ned Flanders"
      IsAdmin = false }

  let private nedReadingAt
    (ranges: ReadingRanges)
    (now: DateTimeOffset)
    (rng: Random)
    (daysAgo: int)
    (sysBase: int)
    (sysJitter: int)
    (diaBase: int)
    (diaJitter: int)
    (comment: string option)
    : BloodPressureReading option =
    let date = now.AddDays(-float daysAgo).Date
    let hour = rng.Next(7, 21)
    let minute = rng.Next(0, 60)

    let ts =
      DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.Zero)

    buildReading
      ranges
      (sysBase + rng.Next(-sysJitter, sysJitter + 1))
      (diaBase + rng.Next(-diaJitter, diaJitter + 1))
      (74 + rng.Next(-5, 6))
      ts
      comment

  // An exact (non-jittered) reading at a fractional days-ago offset, for the Fig. 5
  // values — only HeartRate (absent from the figure) is synthesized.
  let private nedExactReadingAt
    (ranges: ReadingRanges)
    (now: DateTimeOffset)
    (rng: Random)
    (daysAgo: float)
    (systolic: int)
    (diastolic: int)
    (comment: string option)
    : BloodPressureReading option =
    buildReading ranges systolic diastolic (74 + rng.Next(-5, 6)) (now.AddDays(-daysAgo)) comment

  // Transcribed verbatim from the "Blood Pressure Values" data table in Fig. 5
  // (docs/resources/12911_2021_Article_1598.pdf, page 10).
  let private nedSysPreGap = [ 147; 157; 155; 160; 154; 161; 158; 150; 141 ]
  let private nedDiaPreGap = [ 100; 101; 105; 122; 93; 99; 110; 100; 92 ]

  let private nedSysPostGap =
    [ 163
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

  let private nedDiaPostGap =
    [ 106
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

  let private generateNedReadings (ranges: ReadingRanges) (now: DateTimeOffset) : BloodPressureReading list =
    let rng = Random(6)

    // Phase 1 (background, ~1 year): infrequent (every 4 days — under the >10%-of-30-days
    // missing-data threshold, so it never renders dashed), mildly elevated, undiagnosed.
    // Stops a few days before the Fig. 5 window so that the boundary gap stays well under
    // the mission-trip gap, keeping the latter the single largest gap overall.
    let background =
      [ 380..-4..32 ]
      |> List.choose (fun daysAgo -> nedReadingAt ranges now rng daysAgo 141 6 89 5 None)

    // Uniform spacing across the entire 57-point Fig. 5 sequence (matching the figure's
    // roughly daily cadence), compressed to fit a 30-day window with one explicit 5-day
    // gap (4 missing days, matching the figure's "BP cuff broken for four days"
    // annotation exactly). Density must stay uniform on both sides of the gap: LOWESS
    // picks its neighborhood by point rank, not a fixed time window, so a sparse region
    // sitting next to a dense one would get its fit dragged toward the dense side just
    // to find enough neighbors — badly distorting the curve's shape relative to the
    // paper's.
    let preGapCount = nedSysPreGap.Length // 9
    let totalPoints = preGapCount + nedSysPostGap.Length // 57

    // 57 points have 56 gaps between them; one of those gaps is the mission-trip
    // stretch, leaving 55 uniform steps to fill the rest of the 29-day span.
    let bigGapDays = 5.0
    let stepDays = (29.0 - bigGapDays) / float (totalPoints - 2)

    // How much further back a point sits once it's past the gap: the (longer) gap
    // replaces one ordinary step, so everything past it shifts back by the
    // difference. Index j's days-ago is then a single formula: a uniform `j * stepDays`
    // ramp, with that one extra shift applied from the gap onward.
    let gapShift = bigGapDays - stepDays

    let figFiveReadings =
      List.zip3 (nedSysPreGap @ nedSysPostGap) (nedDiaPreGap @ nedDiaPostGap) [ 0 .. totalPoints - 1 ]
      |> List.map (fun (sys, dia, j) ->
        let shift = if j >= preGapCount then gapShift else 0.0
        let daysAgo = 29.0 - float j * stepDays - shift

        let comment =
          if j = preGapCount - 1 then
            Some "Forgot the cuff on a mission trip, diddly!"
          elif j = preGapCount + 17 then
            Some "Started on a little Lisinopril, feeling neighborly!"
          else
            None

        nedExactReadingAt ranges now rng daysAgo sys dia comment)
      |> List.choose id

    background @ figFiveReadings

  // ── public API ────────────────────────────────────────────────────────────

  /// Returns the Simpson family (plus Ned Flanders) as a list of (MemberSpec,
  /// readings) pairs.
  ///
  /// Generation is deterministic: given the same `ranges` and `now`, the result
  /// is always identical. `now` anchors the 5-year window, so trend charts look
  /// current when the demo data is first seeded.
  let simpsons (ranges: ReadingRanges) (now: DateTimeOffset) : (MemberSpec * BloodPressureReading list) list =
    let simpsonFamily =
      profiles |> List.map (fun p -> p.Spec, generateReadings ranges now p)

    simpsonFamily @ [ nedSpec, generateNedReadings ranges now ]
