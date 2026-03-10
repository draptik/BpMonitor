module ReadingsWindowTests

open System
open Xunit
open Swensen.Unquote
open Terminal.Gui.App
open Terminal.Gui.Input
open BpMonitor.Core
open BpMonitor.Tui

let private app = Unchecked.defaultof<IApplication>

let private reading sys dia hr =
  { Id = 0
    Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
    Comments = None }

let private makeRepo (initial: BloodPressureReading list) =
  let mutable data = initial

  { new IReadingRepository with
      member _.GetAll() = data
      member _.Add(r) = data <- data @ [ r ]

      member _.Update(r) =
        data <- data |> List.map (fun x -> if x.Id = r.Id then r else x) }

[<Fact>]
let ``pressing Esc invokes the onQuit callback`` () =
  let quitCalls = ResizeArray<unit>()

  use win =
    new ReadingsWindow(app, makeRepo [], Some(fun () -> quitCalls.Add(())), None, None, None)

  win.NewKeyDownEvent(Key.Esc) |> ignore
  test <@ quitCalls.Count = 1 @>

[<Fact>]
let ``window Readings reflect repository contents`` () =
  let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]
  use win = new ReadingsWindow(app, repo, None, None, None, None)
  test <@ win.Readings.Length = 2 @>

[<Fact>]
let ``window Readings are empty when repository is empty`` () =
  use win = new ReadingsWindow(app, makeRepo [], None, None, None, None)
  test <@ win.Readings.Length = 0 @>

[<Fact>]
let ``AddNew invokes the onAdd callback`` () =
  let addCalls = ResizeArray<unit>()

  use win =
    new ReadingsWindow(
      app,
      makeRepo [],
      None,
      Some(fun () ->
        addCalls.Add(())
        None),
      None,
      None
    )

  win.AddNew()
  test <@ addCalls.Count = 1 @>

[<Fact>]
let ``when onAdd returns a reading it is added to the repository`` () =
  let repo = makeRepo []
  let newReading = reading 120 80 70

  use win =
    new ReadingsWindow(app, repo, None, Some(fun () -> Some newReading), None, None)

  win.AddNew()
  test <@ win.Readings = [ newReading ] @>

[<Fact>]
let ``EditSelected invokes the onEdit callback with the selected reading`` () =
  let editedReadings = ResizeArray<BloodPressureReading>()
  let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]

  use win =
    new ReadingsWindow(
      app,
      repo,
      None,
      None,
      Some(fun r ->
        editedReadings.Add(r)
        None),
      None
    )

  win.EditSelected()
  test <@ editedReadings.Count = 1 && editedReadings[0] = reading 120 80 70 @>

[<Fact>]
let ``EditSelected with empty list does not invoke the onEdit callback`` () =
  let editedReadings = ResizeArray<BloodPressureReading>()

  use win =
    new ReadingsWindow(
      app,
      makeRepo [],
      None,
      None,
      Some(fun r ->
        editedReadings.Add(r)
        None),
      None
    )

  win.EditSelected()
  test <@ editedReadings.Count = 0 @>

[<Fact>]
let ``when onEdit returns an updated reading the repository is updated`` () =
  let repo = makeRepo [ reading 120 80 70 ]
  let updated = reading 135 88 75

  use win =
    new ReadingsWindow(app, repo, None, None, Some(fun _ -> Some updated), None)

  win.EditSelected()
  test <@ win.Readings = [ updated ] @>

[<Fact>]
let ``ShowChart invokes the onChart callback`` () =
  let chartCalls = ResizeArray<unit>()

  use win =
    new ReadingsWindow(app, makeRepo [], None, None, None, Some(fun _ -> chartCalls.Add(())))

  win.ShowChart()
  test <@ chartCalls.Count = 1 @>

[<Fact>]
let ``onChart callback receives all readings from the repository`` () =
  let capturedReadings = ResizeArray<BloodPressureReading list>()
  let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]

  use win =
    new ReadingsWindow(app, repo, None, None, None, Some(fun rs -> capturedReadings.Add(rs)))

  win.ShowChart()
  test <@ capturedReadings.Count = 1 && capturedReadings[0] = repo.GetAll() @>
