module Program

open Terminal.Gui.App

[<EntryPoint>]
let main _ =
    use app = Application.Create()
    app.Init() |> ignore
    use win = new BpMonitor.Tui.DataEntryWindow(app)
    app.Run(win) |> ignore
    0
