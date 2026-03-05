module DataEntryWindowTests

open System
open Xunit
open Swensen.Unquote
open Terminal.Gui.App
open Terminal.Gui.Input
open BpMonitor.Core
open BpMonitor.Tui

let private app = Unchecked.defaultof<IApplication>

let private reading sys dia hr = {
    Id = 0; Systolic = sys; Diastolic = dia; HeartRate = hr
    Timestamp = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
    Comments = None
}

let private makeRepo (initial: BloodPressureReading list) =
    let mutable data = initial
    { new IReadingRepository with
        member _.GetAll() = data
        member _.Add(r)   = data <- data @ [r] }

[<Fact>]
let ``pressing Esc invokes the onQuit callback`` () =
    let quitCalls = ResizeArray<unit>()
    use win = new DataEntryWindow(app, makeRepo [], Some (fun () -> quitCalls.Add(())), None)
    win.NewKeyDownEvent(Key.Esc) |> ignore
    test <@ quitCalls.Count = 1 @>

[<Fact>]
let ``window Readings reflect repository contents`` () =
    let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]
    use win = new DataEntryWindow(app, repo, None, None)
    test <@ win.Readings.Length = 2 @>

[<Fact>]
let ``window Readings are empty when repository is empty`` () =
    use win = new DataEntryWindow(app, makeRepo [], None, None)
    test <@ win.Readings.Length = 0 @>

[<Fact>]
let ``AddNew invokes the onAdd callback`` () =
    let addCalls = ResizeArray<unit>()
    use win = new DataEntryWindow(app, makeRepo [], None, Some (fun () -> addCalls.Add(()); None))
    win.AddNew()
    test <@ addCalls.Count = 1 @>

[<Fact>]
let ``when onAdd returns a reading it is added to the repository`` () =
    let repo = makeRepo []
    let newReading = reading 120 80 70
    use win = new DataEntryWindow(app, repo, None, Some (fun () -> Some newReading))
    win.AddNew()
    test <@ win.Readings = [ newReading ] @>
