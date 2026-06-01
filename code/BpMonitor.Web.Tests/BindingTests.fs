module BindingTests

open Xunit
open FsCheck
open FsCheck.Xunit
open Swensen.Unquote
open BpMonitor.Web

let private form sys dia hr ts comments : Binding.FormModel =
  { Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = ts
    Comments = comments }

[<Fact>]
let ``toUnvalidated parses a well-formed form`` () =
  let m = form "120" "80" "66" "2026-05-01 09:00" "after walk"

  match Binding.toUnvalidated m with
  | Ok u ->
    test <@ u.Systolic = 120 @>
    test <@ u.Diastolic = 80 @>
    test <@ u.HeartRate = 66 @>
    test <@ u.Comments = Some "after walk" @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated maps blank comments to None`` () =
  match Binding.toUnvalidated (form "120" "80" "66" "2026-05-01 09:00" "   ") with
  | Ok u -> test <@ u.Comments = None @>
  | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``toUnvalidated reports a non-numeric systolic`` () =
  match Binding.toUnvalidated (form "abc" "80" "66" "2026-05-01 09:00" "") with
  | Ok _ -> failwith "expected Error"
  | Error errs -> test <@ errs |> List.exists (fun e -> e.Contains "Systolic") @>

[<Fact>]
let ``toUnvalidated accumulates every parse error`` () =
  match Binding.toUnvalidated (form "x" "y" "z" "nope" "") with
  | Ok _ -> failwith "expected Error"
  | Error errs -> test <@ List.length errs = 4 @>

[<Property>]
let ``well-formed numeric strings round-trip to their ints`` (sys: int) (dia: int) (hr: int) =
  let m = form (string sys) (string dia) (string hr) "2026-01-01 00:00" ""

  match Binding.toUnvalidated m with
  | Ok u -> u.Systolic = sys && u.Diastolic = dia && u.HeartRate = hr
  | Error _ -> false
