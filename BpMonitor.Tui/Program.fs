module Program

open System
open Microsoft.Extensions.Configuration
open Terminal.Gui.App
open BpMonitor.Data

[<EntryPoint>]
let main _ =
    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = false)
            .Build()

    let connectionString = config.GetConnectionString("DefaultConnection")

    use app = Application.Create()
    app.Init() |> ignore
    let repository = ReadingRepository.create connectionString
    use win = new BpMonitor.Tui.DataEntryWindow(app, repository, Some (fun () -> app.RequestStop()))
    app.Run(win) |> ignore
    0
