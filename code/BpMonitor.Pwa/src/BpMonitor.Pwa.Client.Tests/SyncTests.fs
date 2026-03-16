module SyncTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Pwa.Client.Reading
open BpMonitor.Pwa.Client.Main

let private noJs = Unchecked.defaultof<Microsoft.JSInterop.IJSRuntime>
let private defaultModel = initModel

let private reading: Reading =
  { Systolic = 120
    Diastolic = 80
    HeartRate = 65
    Timestamp = DateTimeOffset(2026, 3, 16, 9, 0, 0, TimeSpan.Zero)
    Comment = None }

[<Fact>]
let ``DirectoryLoaded true sets HasDirectory`` () =
  let model, _ = update noJs (DirectoryLoaded true) defaultModel
  test <@ model.HasDirectory = true @>

[<Fact>]
let ``DirectoryLoaded false leaves HasDirectory false`` () =
  let model, _ = update noJs (DirectoryLoaded false) defaultModel
  test <@ model.HasDirectory = false @>

[<Fact>]
let ``DirectoryPicked sets HasDirectory`` () =
  let model, _ = update noJs DirectoryPicked defaultModel
  test <@ model.HasDirectory = true @>

[<Fact>]
let ``PushDone sets SyncStatus to SyncDone`` () =
  let model, _ = update noJs PushDone defaultModel
  test <@ model.SyncStatus = SyncDone @>

[<Fact>]
let ``PullDone replaces readings and sets SyncDone`` () =
  let model, _ = update noJs (PullDone [ reading ]) defaultModel
  test <@ model.Readings = [ reading ] && model.SyncStatus = SyncDone @>

[<Fact>]
let ``SyncFailed sets SyncError with message`` () =
  let model, _ = update noJs (SyncFailed "connection refused") defaultModel
  test <@ model.SyncStatus = SyncError "connection refused" @>
