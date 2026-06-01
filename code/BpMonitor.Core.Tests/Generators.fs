module BpMonitor.Core.Tests.Generators

open System
open FsCheck
open FsCheck.FSharp
open BpMonitor.Core

let ranges = ReadingRanges.defaults

/// None, or Some non-empty alphabetic string (never null).
let commentGen: Gen<string option> =
  let someComment =
    Gen.elements [ 'a' .. 'z' ]
    |> Gen.nonEmptyListOf
    |> Gen.map (fun cs -> Some(String(List.toArray cs)))

  Gen.oneof [ Gen.constant None; someComment ]

let timestampGen: Gen<DateTimeOffset> =
  gen {
    let! y = Gen.choose (2000, 2030)
    let! mo = Gen.choose (1, 12)
    let! d = Gen.choose (1, 28)
    let! h = Gen.choose (0, 23)
    let! mi = Gen.choose (0, 59)
    return DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.Zero)
  }

/// All three measurements within their valid ranges.
let validUnvalidatedGen: Gen<BloodPressureReadingUnvalidated> =
  gen {
    let! sys = Gen.choose (ranges.SystolicMin, ranges.SystolicMax)
    let! dia = Gen.choose (ranges.DiastolicMin, ranges.DiastolicMax)
    let! hr = Gen.choose (ranges.HeartRateMin, ranges.HeartRateMax)
    let! ts = timestampGen
    let! comments = commentGen

    return
      { Systolic = sys
        Diastolic = dia
        HeartRate = hr
        Timestamp = ts
        Comments = comments }
  }

/// Measurements straddling the range boundaries (each field may be in or out of range).
let mixedUnvalidatedGen: Gen<BloodPressureReadingUnvalidated> =
  gen {
    let! sys = Gen.choose (ranges.SystolicMin - 50, ranges.SystolicMax + 50)
    let! dia = Gen.choose (ranges.DiastolicMin - 50, ranges.DiastolicMax + 50)
    let! hr = Gen.choose (ranges.HeartRateMin - 50, ranges.HeartRateMax + 50)
    let! ts = timestampGen
    let! comments = commentGen

    return
      { Systolic = sys
        Diastolic = dia
        HeartRate = hr
        Timestamp = ts
        Comments = comments }
  }

/// An int strictly below the minimum or strictly above the maximum.
let outOfRangeGen (lo: int) (hi: int) : Gen<int> =
  Gen.oneof [ Gen.choose (lo - 1000, lo - 1); Gen.choose (hi + 1, hi + 1000) ]
