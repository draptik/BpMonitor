module Program

open System
open Microsoft.Extensions.Configuration
open Terminal.Gui.App
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open BpMonitor.Core
open BpMonitor.Data

let private makeField (y: int) (width: Dim) =
    let f = new TextField(X = Pos.Absolute(20), Y = Pos.Absolute(y), Width = width)
    f.Cursor <- new Cursor(Style = CursorStyle.BlinkingBlock)
    f

let private makeLabel (text: string) (y: int) =
    new Label(Text = text, X = Pos.Absolute(0), Y = Pos.Absolute(y))

let private tryParseInt (label: string) (s: string) =
    match Int32.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error $"{label}: '{s}' is not a valid integer"

let private tryParseTimestamp (s: string) =
    match DateTimeOffset.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error $"Timestamp: '{s}' is not a valid date/time"

let private showAddDialog (app: IApplication) () : BloodPressureReading option =
    let result = ref None

    let systolicField  = makeField 0 (Dim.Absolute(5))
    let diastolicField = makeField 1 (Dim.Absolute(5))
    let heartRateField = makeField 2 (Dim.Absolute(5))
    let timestampField = makeField 3 (Dim.Absolute(16))
    let commentsField  = makeField 4 (Dim.Fill(2))

    timestampField.Text <- DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm")

    let timestampHint =
        new Label(Text = "  (yyyy-MM-dd HH:mm)", X = Pos.Right(timestampField), Y = Pos.Absolute(3))

    let dialog = new Dialog(Title = "Add New Reading", Width = Dim.Percent(60), Height = Dim.Absolute(12))

    let saveButton = new Button(Text = "Save", IsDefault = true)
    saveButton.Accepting.Add(fun _ ->
        let parsed =
            match tryParseInt "Systolic" (string systolicField.Text) with
            | Error e -> Error e
            | Ok sys ->
                match tryParseInt "Diastolic" (string diastolicField.Text) with
                | Error e -> Error e
                | Ok dia ->
                    match tryParseInt "Heart Rate" (string heartRateField.Text) with
                    | Error e -> Error e
                    | Ok hr ->
                        match tryParseTimestamp (string timestampField.Text) with
                        | Error e -> Error e
                        | Ok ts ->
                            let comments =
                                match string commentsField.Text with
                                | "" -> None
                                | s  -> Some s
                            Ok { Systolic = sys; Diastolic = dia; HeartRate = hr; Timestamp = ts; Comments = comments }

        match parsed with
        | Error msg ->
            MessageBox.ErrorQuery(app, "Input Error", msg, "OK") |> ignore
        | Ok unvalidated ->
            match BloodPressureReading.parse ReadingRanges.defaults unvalidated with
            | Ok reading ->
                result.Value <- Some reading
                dialog.RequestStop()
            | Error errors ->
                let msg =
                    errors
                    |> List.map (fun e ->
                        match e with
                        | SystolicOutOfRange v  -> $"Systolic {v} is out of range (1–300)"
                        | DiastolicOutOfRange v  -> $"Diastolic {v} is out of range (1–200)"
                        | HeartRateOutOfRange v  -> $"Heart rate {v} is out of range (1–300)")
                    |> String.concat "\n"
                MessageBox.ErrorQuery(app, "Validation Error", msg, "OK") |> ignore)

    let cancelButton = new Button(Text = "Cancel")
    cancelButton.Accepting.Add(fun _ -> dialog.RequestStop())

    dialog.Add(
        makeLabel "Systolic:"   0, systolicField,
        makeLabel "Diastolic:"  1, diastolicField,
        makeLabel "Heart Rate:" 2, heartRateField,
        makeLabel "Timestamp:"  3, timestampField,
        timestampHint,
        makeLabel "Comments:"   4, commentsField
    )

    dialog.AddButton(saveButton)
    dialog.AddButton(cancelButton)

    app.Run(dialog) |> ignore
    result.Value

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
    use win = new BpMonitor.Tui.DataEntryWindow(app, repository, Some (fun () -> app.RequestStop()), Some (showAddDialog app))
    app.Run(win) |> ignore
    0
