module Generators

open System
open FsCheck
open FsCheck.FSharp
open BpMonitor.Core

let private wordGen: Gen<string> =
  Gen.elements [ 'a' .. 'z' ]
  |> Gen.nonEmptyListOf
  |> Gen.map (List.toArray >> String)

/// None, or Some string of one to three lowercase words joined by single spaces.
/// Such a comment has no newlines, is non-empty, and equals its own Trim(),
/// so it survives MarkdownImport.parseLine's `(.*)$` + Trim() round-trip.
let commentGen: Gen<string option> =
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
    let! memberId = Gen.choose (1, 10)
    let! sys = Gen.choose (1, 300)
    let! dia = Gen.choose (1, 200)
    let! hr = Gen.choose (1, 300)
    let! ts = timestampGen
    let! comments = commentGen
    let! created = timestampGen
    let! modified = timestampGen

    return
      { Id = id
        MemberId = memberId
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

let private toUnvalidated (r: BloodPressureReading) : BloodPressureReadingUnvalidated =
  { Systolic = r.Systolic
    Diastolic = r.Diastolic
    HeartRate = r.HeartRate
    Timestamp = r.Timestamp
    Comments = r.Comments }

/// A reading whose measurements may fall outside ReadingRanges.defaults.
let private mixedReadingGen: Gen<BloodPressureReading> =
  gen {
    let! r = readingGen
    let! sys = Gen.choose (-50, 350)
    let! dia = Gen.choose (-50, 250)
    let! hr = Gen.choose (-50, 350)

    return
      { r with
          Systolic = sys
          Diastolic = dia
          HeartRate = hr }
  }

/// Assigns distinct, increasing timestamps so dedup-by-timestamp is deterministic.
let private withDistinctTimestamps (rs: BloodPressureReading list) =
  rs
  |> List.mapi (fun i r ->
    { r with
        Timestamp = DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(float i) })

/// A list of fully-valid (in-range) readings, each with a distinct timestamp.
let distinctValidReadingsGen: Gen<BloodPressureReading list> =
  Gen.listOf readingGen |> Gen.map withDistinctTimestamps

/// A list of valid in-range unvalidated readings, each with a distinct timestamp.
let distinctValidUnvalidatedGen: Gen<BloodPressureReadingUnvalidated list> =
  distinctValidReadingsGen |> Gen.map (List.map toUnvalidated)

/// A list of unvalidated readings mixing in-range and out-of-range measurements.
let mixedUnvalidatedListGen: Gen<BloodPressureReadingUnvalidated list> =
  Gen.listOf mixedReadingGen |> Gen.map (List.map toUnvalidated)

/// A single source line in a generated markdown document. Each item renders to
/// exactly one line (no embedded newlines), so 1-based line numbers map to indices.
type DocItem =
  /// An ISO date header line, e.g. "2024-10-15".
  | DateHeader of DateOnly
  /// A date header followed by trailing text, e.g. "2024-10-18 day comment".
  | DateHeaderWithText of DateOnly * string
  /// A reading line, e.g. "- 08:30: 120/80 65 after coffee".
  | ReadingRow of hour: int * minute: int * systolic: int * diastolic: int * heartRate: int * comment: string option
  /// A line that is neither a date header nor a reading (headings, comments, blanks).
  | NoiseRow of string

let private renderDate (d: DateOnly) =
  $"%04d{d.Year}-%02d{d.Month}-%02d{d.Day}"

let renderDocLine (item: DocItem) : string =
  match item with
  | DateHeader d -> renderDate d
  | DateHeaderWithText(d, t) -> renderDate d + " " + t
  | ReadingRow(h, m, sys, dia, hr, comment) ->
    let suffix =
      match comment with
      | Some s -> " " + s
      | None -> ""

    $"- %02d{h}:%02d{m}: %d{sys}/%d{dia} %d{hr}%s{suffix}"
  | NoiseRow s -> s

let private wordsGen: Gen<string> =
  Gen.choose (1, 3)
  |> Gen.bind (fun n -> Gen.listOfLength n wordGen)
  |> Gen.map (String.concat " ")

let private dateOnlyGen: Gen<DateOnly> =
  gen {
    let! y = Gen.choose (2000, 2030)
    let! mo = Gen.choose (1, 12)
    let! d = Gen.choose (1, 28)
    return DateOnly(y, mo, d)
  }

let private dateHeaderItemGen: Gen<DocItem> =
  gen {
    let! d = dateOnlyGen
    let! trailing = Gen.oneof [ Gen.constant None; wordsGen |> Gen.map Some ]

    return
      match trailing with
      | Some t -> DateHeaderWithText(d, t)
      | None -> DateHeader d
  }

let private readingItemGen: Gen<DocItem> =
  gen {
    let! h = Gen.choose (0, 23)
    let! m = Gen.choose (0, 59)
    let! sys = Gen.choose (1, 999)
    let! dia = Gen.choose (1, 999)
    let! hr = Gen.choose (1, 999)
    let! comment = commentGen
    return ReadingRow(h, m, sys, dia, hr, comment)
  }

/// Lines that match neither the date nor the reading pattern.
let private noiseItemGen: Gen<DocItem> =
  Gen.elements [ "# heading"; "<!-- ignore -->"; ""; "## section"; "notes here" ]
  |> Gen.map NoiseRow

/// A document as an ordered mix of date headers, reading lines, and noise,
/// weighted towards reading lines.
let docItemsGen: Gen<DocItem list> =
  Gen.frequency [ 2, dateHeaderItemGen; 4, readingItemGen; 2, noiseItemGen ]
  |> Gen.listOf
