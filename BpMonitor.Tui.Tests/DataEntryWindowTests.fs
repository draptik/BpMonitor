module DataEntryWindowTests

open System
open Xunit
open Swensen.Unquote
open Terminal.Gui.App
open Terminal.Gui.Input
open BpMonitor.Core
open BpMonitor.Tui

let private app = Unchecked.defaultof<IApplication>

let private sample = {
    Id = 1; Systolic = 120; Diastolic = 80; HeartRate = 70
    Timestamp = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
    Comments = None
}

[<Fact>]
let ``pressing Esc invokes the onQuit callback`` () =
    let mutable quitCalled = false
    use win = new DataEntryWindow(app, onQuit = fun () -> quitCalled <- true)
    win.NewKeyDownEvent(Key.Esc) |> ignore
    test <@ quitCalled @>

[<Fact>]
let ``window starts with default sample readings`` () =
    use win = new DataEntryWindow(app)
    test <@ win.Readings.Count > 0 @>

[<Fact>]
let ``window uses provided initial readings`` () =
    use win = new DataEntryWindow(app, initialReadings = [ sample ])
    test <@ win.Readings.Count = 1 @>
    test <@ win.Readings.[0].Systolic = 120 @>
