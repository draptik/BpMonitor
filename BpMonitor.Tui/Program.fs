module Program

open Terminal.Gui.App
open BpMonitor.Data

[<EntryPoint>]
let main _ =
    use app = Application.Create()
    app.Init() |> ignore
    let repository = InMemoryReadingRepository(None)
    use win = new BpMonitor.Tui.DataEntryWindow(app, repository, Some (fun () -> app.RequestStop()))
    app.Run(win) |> ignore
    0
