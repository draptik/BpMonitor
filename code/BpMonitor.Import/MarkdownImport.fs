module BpMonitor.Import.MarkdownImport

open BpMonitor.Core

type ImportSummary =
  { Added: int
    Updated: int
    Failed: (int * string * BloodPressureReadingUnvalidated * ValidationError list) list }

open System
open BpMonitor.Core

/// Parses a single markdown list item (e.g. "- 08:30: 120/80 65 comment") into an unvalidated reading.
/// Returns None if the line does not match the expected format.
let parseLine (date: DateOnly) (line: string) : BloodPressureReadingUnvalidated option =
  let readingPattern =
    System.Text.RegularExpressions.Regex(@"^- (\d{1,2})[.:](\d{2}): (\d+)/(\d+) (\d+)(.*)$")

  let m = readingPattern.Match(line)

  if m.Success then
    let hour = int m.Groups[1].Value
    let minute = int m.Groups[2].Value
    let systolic = int m.Groups[3].Value
    let diastolic = int m.Groups[4].Value
    let heartRate = int m.Groups[5].Value
    let comment = m.Groups[6].Value.Trim()

    let timestamp =
      DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.Zero)

    Some
      { Systolic = systolic
        Diastolic = diastolic
        HeartRate = heartRate
        Timestamp = timestamp
        Comments = if comment = "" then None else Some comment }
  else
    None

/// Parses a full markdown document into a list of (1-based line number, original line, unvalidated reading) triples.
/// Scans lines sequentially: a line matching an ISO date header (e.g. "2024-03-01") sets the current date context;
/// every subsequent non-date line is attempted as a reading using that date, and silently skipped if it does not match.
/// The date context carries forward until the next date header.
let parseMarkdown (markdown: string) : (int * string * BloodPressureReadingUnvalidated) list =
  let datePattern = System.Text.RegularExpressions.Regex(@"^(\d{4}-\d{2}-\d{2})")

  let lines = markdown.Split([| '\n'; '\r' |], StringSplitOptions.None)

  let folder (currentDate, readings) (lineIndex: int, line: string) =
    let dm = datePattern.Match(line)

    if dm.Success then
      let date = DateOnly.Parse(dm.Groups[1].Value)
      (Some date, readings)
    else
      match currentDate with
      | None -> (None, readings)
      | Some date ->
        match parseLine date line with
        | None -> (currentDate, readings)
        | Some reading -> (currentDate, (lineIndex + 1, line, reading) :: readings)

  lines |> Array.indexed |> Array.fold folder (None, []) |> snd |> List.rev

let import
  (repository: IReadingRepository)
  (ranges: ReadingRanges)
  (unvalidated: (int * string * BloodPressureReadingUnvalidated) list)
  : ImportSummary =
  let existing = repository.GetAll()

  let folder acc (lineNumber, line, reading) =
    let (added, updated, failed) = acc

    match BloodPressureReading.parse ranges reading with
    | Error errors -> (added, updated, (lineNumber, line, reading, errors) :: failed)
    | Ok validated ->
      match existing |> List.tryFind (fun r -> r.Timestamp = validated.Timestamp) with
      | None ->
        repository.Add(validated)
        (added + 1, updated, failed)
      | Some existing ->
        repository.Update({ validated with Id = existing.Id })
        (added, updated + 1, failed)

  let (added, updated, failed) = unvalidated |> List.fold folder (0, 0, [])

  { Added = added
    Updated = updated
    Failed = List.rev failed }
