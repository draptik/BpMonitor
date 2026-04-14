module ReadingsWindowTests

open System
open Xunit
open Swensen.Unquote
open Terminal.Gui.App
open Terminal.Gui.Input
open BpMonitor.Core
open BpMonitor.Import.MarkdownImport
open BpMonitor.Tui

let private app = Unchecked.defaultof<IApplication>

let private reading sys dia hr =
  { Id = 0
    Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = Timestamp.utc 2026 1 1 9 0 0
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private readingAt sys dia hr (ts: DateTimeOffset) =
  { reading sys dia hr with
      Timestamp = ts }

let private makeRepo (initial: BloodPressureReading list) =
  let mutable data = initial

  { new IReadingRepository with
      member _.GetAll() = data
      member _.Add(r) = data <- data @ [ r ]
      member _.AddMany(rs) = data <- data @ rs

      member _.Update(r) =
        data <- data |> List.map (fun x -> if x.Id = r.Id then r else x) }

type private Callbacks =
  { OnQuit: (unit -> unit) option
    OnAdd: (unit -> BloodPressureReading option) option
    OnEdit: (BloodPressureReading -> BloodPressureReading option) option
    OnChart: (BloodPressureReading list -> unit) option
    OnImport: (unit -> ImportSummary option) option
    OnSave: (unit -> Result<unit, string>) option }

let private noCallbacks =
  { OnQuit = None
    OnAdd = None
    OnEdit = None
    OnChart = None
    OnImport = None
    OnSave = None }

let private makeWin repo (cb: Callbacks) =
  new ReadingsWindow(app, repo, cb.OnQuit, cb.OnAdd, cb.OnEdit, cb.OnChart, cb.OnImport, cb.OnSave)

[<Fact>]
let ``pressing Esc invokes the onQuit callback`` () =
  let quitCalls = ResizeArray<unit>()

  use win =
    makeWin
      (makeRepo [])
      { noCallbacks with
          OnQuit = Some(fun () -> quitCalls.Add(())) }

  win.NewKeyDownEvent(Key.Esc) |> ignore
  test <@ quitCalls.Count = 1 @>

[<Fact>]
let ``window Readings reflect repository contents`` () =
  let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]
  use win = makeWin repo noCallbacks
  test <@ win.Readings.Length = 2 @>

[<Fact>]
let ``window Readings are empty when repository is empty`` () =
  use win = makeWin (makeRepo []) noCallbacks
  test <@ win.Readings.Length = 0 @>

[<Fact>]
let ``AddNew invokes the onAdd callback`` () =
  let addCalls = ResizeArray<unit>()

  use win =
    makeWin
      (makeRepo [])
      { noCallbacks with
          OnAdd =
            Some(fun () ->
              addCalls.Add(())
              None) }

  win.AddNew()
  test <@ addCalls.Count = 1 @>

[<Fact>]
let ``when onAdd returns a reading it is added to the repository`` () =
  let repo = makeRepo []
  let newReading = reading 120 80 70

  use win =
    makeWin
      repo
      { noCallbacks with
          OnAdd = Some(fun () -> Some newReading) }

  win.AddNew()
  test <@ win.Readings = [ newReading ] @>

[<Fact>]
let ``EditSelected invokes the onEdit callback with the selected reading`` () =
  let editedReadings = ResizeArray<BloodPressureReading>()
  let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]

  use win =
    makeWin
      repo
      { noCallbacks with
          OnEdit =
            Some(fun r ->
              editedReadings.Add(r)
              None) }

  win.EditSelected()
  test <@ editedReadings.Count = 1 && editedReadings[0] = reading 120 80 70 @>

[<Fact>]
let ``EditSelected with empty list does not invoke the onEdit callback`` () =
  let editedReadings = ResizeArray<BloodPressureReading>()

  use win =
    makeWin
      (makeRepo [])
      { noCallbacks with
          OnEdit =
            Some(fun r ->
              editedReadings.Add(r)
              None) }

  win.EditSelected()
  test <@ editedReadings.Count = 0 @>

[<Fact>]
let ``when onEdit returns an updated reading the repository is updated`` () =
  let repo = makeRepo [ reading 120 80 70 ]
  let updated = reading 135 88 75

  use win =
    makeWin
      repo
      { noCallbacks with
          OnEdit = Some(fun _ -> Some updated) }

  win.EditSelected()
  test <@ win.Readings = [ updated ] @>

[<Fact>]
let ``ShowChart invokes the onChart callback`` () =
  let chartCalls = ResizeArray<unit>()

  use win =
    makeWin
      (makeRepo [])
      { noCallbacks with
          OnChart = Some(fun _ -> chartCalls.Add(())) }

  win.ShowChart()
  test <@ chartCalls.Count = 1 @>

[<Fact>]
let ``onChart callback receives all readings from the repository`` () =
  let capturedReadings = ResizeArray<BloodPressureReading list>()
  let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]

  use win =
    makeWin
      repo
      { noCallbacks with
          OnChart = Some(fun rs -> capturedReadings.Add(rs)) }

  win.ShowChart()
  test <@ capturedReadings.Count = 1 && capturedReadings[0] = repo.GetAll() @>

[<Fact>]
let ``SaveFile invokes the onSave callback`` () =
  let saveCalls = ResizeArray<unit>()

  use win =
    makeWin
      (makeRepo [])
      { noCallbacks with
          OnSave =
            Some(fun () ->
              saveCalls.Add(())
              Ok()) }

  win.SaveFile()
  test <@ saveCalls.Count = 1 @>

[<Fact>]
let ``EditSelected picks newest reading first when entries have different timestamps`` () =
  let older = readingAt 120 80 70 (Timestamp.utc 2026 1 1 9 0 0)
  let newer = readingAt 130 85 72 (Timestamp.utc 2026 1 2 9 0 0)
  let editedReadings = ResizeArray<BloodPressureReading>()
  let repo = makeRepo [ older; newer ] // older is first in repo

  use win =
    makeWin
      repo
      { noCallbacks with
          OnEdit =
            Some(fun r ->
              editedReadings.Add(r)
              None) }

  // row 0 should map to newest reading (sorted descending)
  win.EditSelected()
  test <@ editedReadings.Count = 1 && editedReadings[0] = newer @>

[<Fact>]
let ``MoveDown moves selection to the next row`` () =
  let repo =
    makeRepo
      [ readingAt 120 80 70 (Timestamp.utc 2026 1 2 9 0 0)
        readingAt 130 85 72 (Timestamp.utc 2026 1 1 9 0 0) ]

  use win = makeWin repo noCallbacks
  test <@ win.SelectedRow = 0 @>
  win.MoveDown()
  test <@ win.SelectedRow = 1 @>

[<Fact>]
let ``MoveUp moves selection to the previous row`` () =
  let repo =
    makeRepo
      [ readingAt 120 80 70 (Timestamp.utc 2026 1 2 9 0 0)
        readingAt 130 85 72 (Timestamp.utc 2026 1 1 9 0 0) ]

  use win = makeWin repo noCallbacks
  win.MoveDown()
  test <@ win.SelectedRow = 1 @>
  win.MoveUp()
  test <@ win.SelectedRow = 0 @>

[<Fact>]
let ``MoveDown at the last row stays at the last row`` () =
  let repo = makeRepo [ reading 120 80 70 ]
  use win = makeWin repo noCallbacks
  win.MoveDown()
  test <@ win.SelectedRow = 0 @>

[<Fact>]
let ``MoveUp at the first row stays at the first row`` () =
  let repo = makeRepo [ reading 120 80 70 ]
  use win = makeWin repo noCallbacks
  win.MoveUp()
  test <@ win.SelectedRow = 0 @>

[<Fact>]
let ``ImportFile invokes the onImport callback`` () =
  let importCalls = ResizeArray<unit>()

  use win =
    makeWin
      (makeRepo [])
      { noCallbacks with
          OnImport =
            Some(fun () ->
              importCalls.Add(())
              None) }

  win.ImportFile()
  test <@ importCalls.Count = 1 @>
