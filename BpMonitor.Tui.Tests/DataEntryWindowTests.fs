module DataEntryWindowTests

open Xunit
open Swensen.Unquote
open Terminal.Gui.App
open Terminal.Gui.Input
open BpMonitor.Tui

[<Fact>]
let ``pressing Esc invokes the onQuit callback`` () =
    let mutable quitCalled = false
    use win = new DataEntryWindow(Unchecked.defaultof<IApplication>, onQuit = fun () -> quitCalled <- true)
    win.NewKeyDownEvent(Key.Esc) |> ignore
    test <@ quitCalled @>
