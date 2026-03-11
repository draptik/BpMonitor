module BpMonitor.Import.MarkdownImport

open BpMonitor.Core

type ImportSummary =
  { Added: int
    Updated: int
    Failed: (BloodPressureReadingUnvalidated * ValidationError list) list }

open System
open BpMonitor.Core

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

let parseMarkdown (markdown: string) : BloodPressureReadingUnvalidated list =
  let datePattern = System.Text.RegularExpressions.Regex(@"^(\d{4}-\d{2}-\d{2})")

  let lines = markdown.Split([| '\n'; '\r' |], StringSplitOptions.None)

  let folder (currentDate, readings) (line: string) =
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
        | Some reading -> (currentDate, readings @ [ reading ])

  lines |> Array.fold folder (None, []) |> snd

let import
  (repository: IReadingRepository)
  (ranges: ReadingRanges)
  (unvalidated: BloodPressureReadingUnvalidated list)
  : ImportSummary =
  let existing = repository.GetAll()

  let mutable added = 0
  let mutable updated = 0
  let mutable failed = []

  for reading in unvalidated do
    match BloodPressureReading.parse ranges reading with
    | Error errors -> failed <- failed @ [ (reading, errors) ]
    | Ok validated ->
      match existing |> List.tryFind (fun r -> r.Timestamp = validated.Timestamp) with
      | None ->
        repository.Add(validated)
        added <- added + 1
      | Some existing ->
        repository.Update({ validated with Id = existing.Id })
        updated <- updated + 1

  { Added = added
    Updated = updated
    Failed = failed }
