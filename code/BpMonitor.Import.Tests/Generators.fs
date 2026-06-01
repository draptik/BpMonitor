module BpMonitor.Import.Tests.Generators

open System
open FsCheck
open FsCheck.FSharp
open BpMonitor.Core

/// None, or Some string of one to three lowercase words joined by single spaces.
/// Such a comment has no newlines, is non-empty, and equals its own Trim(),
/// so it survives MarkdownImport.parseLine's `(.*)$` + Trim() round-trip.
let commentGen: Gen<string option> =
  let wordGen =
    Gen.elements [ 'a' .. 'z' ]
    |> Gen.nonEmptyListOf
    |> Gen.map (List.toArray >> String)

  let someComment =
    Gen.choose (1, 3)
    |> Gen.bind (fun n -> Gen.listOfLength n wordGen)
    |> Gen.map (fun ws -> Some(String.concat " " ws))

  Gen.oneof [ Gen.constant None; someComment ]

/// A DateTimeOffset with a whole-second time and an hour-aligned offset, which
/// round-trips exactly through System.Text.Json.
let timestampGen: Gen<DateTimeOffset> =
  gen {
    let! y = Gen.choose (2000, 2030)
    let! mo = Gen.choose (1, 12)
    let! d = Gen.choose (1, 28)
    let! h = Gen.choose (0, 23)
    let! mi = Gen.choose (0, 59)
    let! s = Gen.choose (0, 59)
    let! offsetHours = Gen.choose (-12, 12)
    return DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.FromHours(float offsetHours))
  }

/// A fully-populated, validated reading for JSON round-trip testing.
/// Comments are never `Some null` (which FSharp.SystemTextJson would collapse to None).
let readingGen: Gen<BloodPressureReading> =
  gen {
    let! id = Gen.choose (0, 100000)
    let! sys = Gen.choose (1, 300)
    let! dia = Gen.choose (1, 200)
    let! hr = Gen.choose (1, 300)
    let! ts = timestampGen
    let! comments = commentGen
    let! created = timestampGen
    let! modified = timestampGen

    return
      { Id = id
        Systolic = sys
        Diastolic = dia
        HeartRate = hr
        Timestamp = ts
        Comments = comments
        CreatedAt = created
        ModifiedAt = modified }
  }

/// The raw ingredients of a single markdown reading line.
type MarkdownLineCase =
  { Date: DateOnly
    Hour: int
    Minute: int
    Systolic: int
    Diastolic: int
    HeartRate: int
    Comment: string option }

let markdownLineGen: Gen<MarkdownLineCase> =
  gen {
    let! y = Gen.choose (2000, 2030)
    let! mo = Gen.choose (1, 12)
    let! d = Gen.choose (1, 28)
    let! h = Gen.choose (0, 23)
    let! mi = Gen.choose (0, 59)
    let! sys = Gen.choose (1, 999)
    let! dia = Gen.choose (1, 999)
    let! hr = Gen.choose (1, 999)
    let! comment = commentGen

    return
      { Date = DateOnly(y, mo, d)
        Hour = h
        Minute = mi
        Systolic = sys
        Diastolic = dia
        HeartRate = hr
        Comment = comment }
  }
