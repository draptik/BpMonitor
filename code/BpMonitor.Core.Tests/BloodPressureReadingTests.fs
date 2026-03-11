module BloodPressureReadingTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core

let private timeProvider =
  FakeTimeProvider(DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero))

let private validUnvalidated: BloodPressureReadingUnvalidated =
  { Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = timeProvider.GetUtcNow()
    Comments = None }

let private ranges = ReadingRanges.defaults

[<Fact>]
let ``parse returns Ok when input is valid`` () =
  test <@ BloodPressureReading.parse ranges validUnvalidated |> Result.isOk @>

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
