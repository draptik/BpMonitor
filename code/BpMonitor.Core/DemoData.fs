namespace BpMonitor.Core

open System

/// A lightweight descriptor for a family member used by the demo-data generator.
/// The seeder (BpMonitor.Data.DemoSeeder) converts these into real FamilyMember records.
type MemberSpec = { Name: string; IsAdmin: bool }

/// Deterministic demo-data generator — the Simpson family.
///
/// All generation is pure (no I/O). A per-member fixed Random seed ensures
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

          let sys =
            clamp
              ranges.SystolicMin
              ranges.SystolicMax
              (profile.SysBase + rng.Next(-profile.SysJitter, profile.SysJitter + 1))

          let dia =
            clamp
              ranges.DiastolicMin
              ranges.DiastolicMax
              (profile.DiaBase + rng.Next(-profile.DiaJitter, profile.DiaJitter + 1))

          let hr =
            clamp
              ranges.HeartRateMin
              ranges.HeartRateMax
              (profile.HrBase + rng.Next(-profile.HrJitter, profile.HrJitter + 1))

          let commentIdx = rng.Next(0, profile.Comments.Length)
          let comment = profile.Comments[commentIdx]

          let unvalidated =
            { Systolic = sys
              Diastolic = dia
              HeartRate = hr
              Timestamp = ts
              Comments = comment }

          match BloodPressureReading.parse ranges unvalidated with
          | Ok r -> Some r
          | Error _ -> None // never reached: values are clamped into range
    )

  // ── public API ────────────────────────────────────────────────────────────

  /// Returns the Simpson family as a list of (MemberSpec, readings) pairs.
  ///
  /// Generation is deterministic: given the same `ranges` and `now`, the result
  /// is always identical. `now` anchors the 5-year window so trend charts look
  /// current when the demo data is first seeded.
  let simpsons (ranges: ReadingRanges) (now: DateTimeOffset) : (MemberSpec * BloodPressureReading list) list =
    profiles |> List.map (fun p -> p.Spec, generateReadings ranges now p)
