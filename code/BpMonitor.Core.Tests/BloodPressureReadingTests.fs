module BloodPressureReadingTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core

let private timeProvider = FakeTimeProvider(Timestamp.utc 2026 3 3 0 0 0)

let private validUnvalidated: BloodPressureReadingUnvalidated =
  { Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = timeProvider.GetUtcNow()
    Comments = None }

let private ranges = ReadingRanges.defaults

[<Fact>]
let ``formatLocal is unaffected by the ambient culture`` () =
  let ts = Timestamp.local 2026 3 3 9 5 0
  let original = Threading.Thread.CurrentThread.CurrentCulture

  try
    let invariant = Formats.formatLocal ts
    Threading.Thread.CurrentThread.CurrentCulture <- Globalization.CultureInfo("ar-SA")
    test <@ Formats.formatLocal ts = invariant @>
  finally
    Threading.Thread.CurrentThread.CurrentCulture <- original

[<Fact>]
let ``formatDate is unaffected by the ambient culture`` () =
  let d = DateOnly(2026, 3, 3)
  let original = Threading.Thread.CurrentThread.CurrentCulture

  try
    let invariant = Formats.formatDate d
    Threading.Thread.CurrentThread.CurrentCulture <- Globalization.CultureInfo("ar-SA")
    test <@ Formats.formatDate d = invariant @>
  finally
    Threading.Thread.CurrentThread.CurrentCulture <- original

[<Fact>]
let ``parse returns Ok when input is valid`` () =
  test <@ BloodPressureReading.parse ranges validUnvalidated |> Result.isOk @>

[<Fact>]
let ``parse accepts systolic exactly at the min and max boundaries`` () =
  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            Systolic = ranges.SystolicMin }
      |> Result.isOk
    @>

  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            Systolic = ranges.SystolicMax }
      |> Result.isOk
    @>

[<Fact>]
let ``parse accepts diastolic exactly at the min and max boundaries`` () =
  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            Diastolic = ranges.DiastolicMin }
      |> Result.isOk
    @>

  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            Diastolic = ranges.DiastolicMax }
      |> Result.isOk
    @>

[<Fact>]
let ``parse accepts heart rate exactly at the min and max boundaries`` () =
  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            HeartRate = ranges.HeartRateMin }
      |> Result.isOk
    @>

  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            HeartRate = ranges.HeartRateMax }
      |> Result.isOk
    @>

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(301)>]
let ``parse returns Error when systolic is out of range`` (invalidSystolic: int) =
  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            Systolic = invalidSystolic }
      |> Result.isError
    @>

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(201)>]
let ``parse returns Error when diastolic is out of range`` (invalidDiastolic: int) =
  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            Diastolic = invalidDiastolic }
      |> Result.isError
    @>

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(301)>]
let ``parse returns Error when heart rate is out of range`` (invalidHeartRate: int) =
  test
    <@
      BloodPressureReading.parse
        ranges
        { validUnvalidated with
            HeartRate = invalidHeartRate }
      |> Result.isError
    @>

[<Fact>]
let ``parse collects all validation errors`` () =
  let allInvalid =
    { validUnvalidated with
        Systolic = 0
        Diastolic = 0
        HeartRate = 0 }

  match BloodPressureReading.parse ranges allInvalid with
  | Error errors -> test <@ errors.Length = 3 @>
  | Ok _ -> failwith "Expected Error"

[<Fact>]
let ``parse sets CreatedAt and ModifiedAt to MinValue`` () =
  match BloodPressureReading.parse ranges validUnvalidated with
  | Ok reading ->
    test <@ reading.CreatedAt = DateTimeOffset.MinValue @>
    test <@ reading.ModifiedAt = DateTimeOffset.MinValue @>
  | Error _ -> failwith "Expected Ok"

[<Fact>]
let ``parse sets MemberId to 0`` () =
  match BloodPressureReading.parse ranges validUnvalidated with
  | Ok reading -> test <@ reading.MemberId = 0 @>
  | Error _ -> failwith "Expected Ok"

[<Fact>]
let ``formatDateEuropean renders as dd.MM.yyyy`` () =
  test <@ Formats.formatDateEuropean (DateOnly(2026, 4, 1)) = "01.04.2026" @>
