module Program

open Terminal.Gui.App

[<EntryPoint>]
let main _ =
    use app = Application.Create()
    app.Init() |> ignore
    use win = new BpMonitor.Tui.DataEntryWindow(app, onQuit = fun () -> app.RequestStop())
    app.Run(win) |> ignore
    0
