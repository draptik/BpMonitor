module BpMonitor.Core.Tests.BloodPressureReadingPropertyTests

open System
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open BpMonitor.Core.Tests.Generators

let private inRange lo hi v = v >= lo && v <= hi

[<Property>]
let ``valid input parses to Ok preserving all fields`` () =
  Prop.forAll (Arb.fromGen validUnvalidatedGen) (fun input ->
    match BloodPressureReading.parse ranges input with
    | Ok r ->
      r.Systolic = input.Systolic
      && r.Diastolic = input.Diastolic
      && r.HeartRate = input.HeartRate
      && r.Timestamp = input.Timestamp
      && r.Comments = input.Comments
    | Error _ -> false)

[<Property>]
let ``valid input always has Id 0 and MinValue audit timestamps`` () =
  Prop.forAll (Arb.fromGen validUnvalidatedGen) (fun input ->
    match BloodPressureReading.parse ranges input with
    | Ok r ->
      r.Id = 0
      && r.CreatedAt = DateTimeOffset.MinValue
      && r.ModifiedAt = DateTimeOffset.MinValue
    | Error _ -> false)

[<Property>]
let ``systolic out of range yields exactly SystolicOutOfRange`` () =
  let arb =
    Arb.fromGen (Gen.zip validUnvalidatedGen (outOfRangeGen ranges.SystolicMin ranges.SystolicMax))

  Prop.forAll arb (fun (baseInput, badSys) ->
    let input = { baseInput with Systolic = badSys }

    match BloodPressureReading.parse ranges input with
    | Error errs -> errs = [ SystolicOutOfRange badSys ]
    | Ok _ -> false)

[<Property>]
let ``diastolic out of range yields exactly DiastolicOutOfRange`` () =
  let arb =
    Arb.fromGen (Gen.zip validUnvalidatedGen (outOfRangeGen ranges.DiastolicMin ranges.DiastolicMax))

  Prop.forAll arb (fun (baseInput, badDia) ->
    let input = { baseInput with Diastolic = badDia }

    match BloodPressureReading.parse ranges input with
    | Error errs -> errs = [ DiastolicOutOfRange badDia ]
    | Ok _ -> false)

[<Property>]
let ``heart rate out of range yields exactly HeartRateOutOfRange`` () =
  let arb =
    Arb.fromGen (Gen.zip validUnvalidatedGen (outOfRangeGen ranges.HeartRateMin ranges.HeartRateMax))

  Prop.forAll arb (fun (baseInput, badHr) ->
    let input = { baseInput with HeartRate = badHr }

    match BloodPressureReading.parse ranges input with
    | Error errs -> errs = [ HeartRateOutOfRange badHr ]
    | Ok _ -> false)

[<Property>]
let ``error count equals number of out-of-range fields`` () =
  Prop.forAll (Arb.fromGen mixedUnvalidatedGen) (fun input ->
    let expected =
      (if inRange ranges.SystolicMin ranges.SystolicMax input.Systolic then
         0
       else
         1)
      + (if inRange ranges.DiastolicMin ranges.DiastolicMax input.Diastolic then
           0
         else
           1)
      + (if inRange ranges.HeartRateMin ranges.HeartRateMax input.HeartRate then
           0
         else
           1)

    match BloodPressureReading.parse ranges input with
    | Ok _ -> expected = 0
    | Error errs -> List.length errs = expected)
