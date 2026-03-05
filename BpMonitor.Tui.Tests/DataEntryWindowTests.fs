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
    let mutable quitCalled = false
    use win = new DataEntryWindow(app, makeRepo [], Some (fun () -> quitCalled <- true))
    win.NewKeyDownEvent(Key.Esc) |> ignore
    test <@ quitCalled @>

[<Fact>]
let ``window Readings reflect repository contents`` () =
    let repo = makeRepo [ reading 120 80 70; reading 130 85 72 ]
    use win = new DataEntryWindow(app, repo, None)
    test <@ win.Readings.Length = 2 @>

[<Fact>]
let ``window Readings are empty when repository is empty`` () =
    use win = new DataEntryWindow(app, makeRepo [], None)
    test <@ win.Readings.Length = 0 @>
