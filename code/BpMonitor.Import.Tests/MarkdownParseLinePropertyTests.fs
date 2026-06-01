module BpMonitor.Import.Tests.MarkdownParseLinePropertyTests

open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open BpMonitor.Import.MarkdownImport
open BpMonitor.Import.Tests.Generators

let private renderLine (c: MarkdownLineCase) =
  let suffix =
    match c.Comment with
    | Some s -> " " + s
    | None -> ""

  $"- %02d{c.Hour}:%02d{c.Minute}: %d{c.Systolic}/%d{c.Diastolic} %d{c.HeartRate}%s{suffix}"

[<Property>]
let ``parseLine round-trips a rendered reading line`` () =
  Prop.forAll (Arb.fromGen markdownLineGen) (fun c ->
    let line = renderLine c

    let expected: BloodPressureReadingUnvalidated =
      { Systolic = c.Systolic
        Diastolic = c.Diastolic
        HeartRate = c.HeartRate
        Timestamp = Timestamp.local c.Date.Year c.Date.Month c.Date.Day c.Hour c.Minute 0
        Comments = c.Comment }

    parseLine c.Date line = Some expected)
