module BindingTests

open System
open Xunit
open FsCheck.FSharp
open FsCheck.Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.TestSupport
open BpMonitor.Web

let private form sys dia hr ts comments : Binding.FormModel =
  {
    Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = ts
    Comments = comments
    ReturnTo = ""
  }

[<Fact>]
let ``toUnvalidated parses a well-formed form`` () =
  let m = form "120" "80" "66" "2026-05-01 09:00" "after walk"

  match Binding.toUnvalidated LocalizedStrings.en m with
  | Ok u ->
    test <@ u.Systolic = 120 @>
    test <@ u.Diastolic = 80 @>
    test <@ u.HeartRate = 66 @>
    test <@ u.Comments = Some "after walk" @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated maps blank comments to None`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "120" "80" "66" "2026-05-01 09:00" "   ") with
  | Ok u -> test <@ u.Comments = None @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated reports a non-numeric systolic`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "abc" "80" "66" "2026-05-01 09:00" "") with
  | Ok _ -> failwith "expected Error"
  | Error errs -> test <@ errs |> List.exists (fun e -> e.Contains "Systolic") @>

[<Fact>]
let ``toUnvalidated accumulates every parse error`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "x" "y" "z" "nope" "") with
  | Ok _ -> failwith "expected Error"
  | Error errs -> test <@ List.length errs = 4 @>

[<Fact>]
let ``toUnvalidated accepts a negative systolic value — range checks are BloodPressureReading.parse's job`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "-5" "80" "66" "2026-05-01 09:00" "") with
  | Ok u -> test <@ u.Systolic = -5 @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated accepts a date-only timestamp with no time component`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "120" "80" "66" "2026-05-01" "") with
  | Ok u -> test <@ u.Timestamp.Hour = 0 && u.Timestamp.Minute = 0 @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated reads the timestamp in the Gregorian calendar, whatever the server culture`` () =
  let result =
    CultureScope.withCulture "th-TH" (fun () ->
      Binding.toUnvalidated LocalizedStrings.en (form "120" "80" "66" "2026-05-01 09:00" ""))

  match result with
  | Ok u -> test <@ (u.Timestamp.Year, u.Timestamp.Month, u.Timestamp.Day) = (2026, 5, 1) @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated rejects a timestamp outside the yyyy-MM-dd HH:mm format, such as a dotted date`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "120" "80" "66" "01.05.2026 09:00" "") with
  | Ok u -> failwith $"expected Error, got {u.Timestamp}"
  | Error errs -> test <@ errs |> List.exists (fun e -> e.Contains "01.05.2026 09:00") @>

[<Fact>]
let ``toUnvalidated names the expected timestamp format in its error`` () =
  match Binding.toUnvalidated LocalizedStrings.en (form "120" "80" "66" "01.05.2026 09:00" "") with
  | Ok u -> failwith $"expected Error, got {u.Timestamp}"
  | Error errs -> test <@ errs |> List.exists (fun e -> e.Contains "(expected yyyy-MM-dd HH:mm)") @>

[<Property>]
let ``well-formed numeric strings round-trip to their ints`` (sys: int) (dia: int) (hr: int) =
  let m = form (string sys) (string dia) (string hr) "2026-01-01 00:00" ""

  match Binding.toUnvalidated LocalizedStrings.en m with
  | Ok u -> u.Systolic = sys && u.Diastolic = dia && u.HeartRate = hr
  | Error _ -> false

// A minute-precision local time and how it was typed, padded with spaces or tabs.
let private typedTimestampGen =
  gen {
    let! year = Gen.choose (2000, 2099)
    let! month = Gen.choose (1, 12)
    let! day = Gen.choose (1, DateTime.DaysInMonth(year, month))
    let! hour = Gen.choose (0, 23)
    let! minute = Gen.choose (0, 59)
    let dt = DateTime(year, month, day, hour, minute, 0)

    let padding =
      Gen.elements [ ' '; '\t' ] |> Gen.listOf |> Gen.map (List.toArray >> String)

    let! before = padding
    let! after = padding

    return
      dt,
      before
      + dt.ToString(Formats.timestamp, Globalization.CultureInfo.InvariantCulture)
      + after
  }

[<Property>]
let ``toUnvalidated reads any well-formed timestamp as that wall-clock time in the server's offset`` () =
  Prop.forAll (Arb.fromGen typedTimestampGen) (fun (dt, typed) ->
    match Binding.toUnvalidated LocalizedStrings.en (form "120" "80" "66" typed "") with
    | Ok u ->
      u.Timestamp.DateTime = dt
      && u.Timestamp.Offset = TimeZoneInfo.Local.GetUtcOffset dt
    | Error _ -> false)
