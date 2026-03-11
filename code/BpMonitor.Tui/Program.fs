module Program

open System
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Configuration
open Terminal.Gui.App
open Terminal.Gui.Drivers
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open System.Diagnostics
open System.IO
open BpMonitor.Charts
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Import.MarkdownImport

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

let private parseFields systolic diastolic heartRate timestamp comments =
  result {
    let! sys = tryParseInt "Systolic" systolic
    let! dia = tryParseInt "Diastolic" diastolic
    let! hr = tryParseInt "Heart Rate" heartRate
    let! ts = tryParseTimestamp timestamp

    let comments =
      match comments with
      | "" -> None
      | s -> Some s

    return
      { Systolic = sys
        Diastolic = dia
        HeartRate = hr
        Timestamp = ts
        Comments = comments }
  }

let private readRanges (config: IConfiguration) =
  let s = config.GetSection("ReadingRanges")
  let d = ReadingRanges.defaults

  let getInt key fallback =
    match s.[key] with
    | null -> fallback
    | v ->
      match Int32.TryParse(v) with
      | true, n -> n
      | _ -> fallback

  { SystolicMin = getInt "SystolicMin" d.SystolicMin
    SystolicMax = getInt "SystolicMax" d.SystolicMax
    DiastolicMin = getInt "DiastolicMin" d.DiastolicMin
    DiastolicMax = getInt "DiastolicMax" d.DiastolicMax
    HeartRateMin = getInt "HeartRateMin" d.HeartRateMin
    HeartRateMax = getInt "HeartRateMax" d.HeartRateMax }

let private formatValidationErrors (ranges: ReadingRanges) (errors: ValidationError list) =
  errors
  |> List.map (fun e ->
    match e with
    | SystolicOutOfRange v -> $"Systolic {v} is out of range ({ranges.SystolicMin}–{ranges.SystolicMax})"
    | DiastolicOutOfRange v -> $"Diastolic {v} is out of range ({ranges.DiastolicMin}–{ranges.DiastolicMax})"
    | HeartRateOutOfRange v -> $"Heart rate {v} is out of range ({ranges.HeartRateMin}–{ranges.HeartRateMax})")
  |> String.concat "\n"

let private showReadingDialog
  (app: IApplication)
  (ranges: ReadingRanges)
  (title: string)
  (existing: BloodPressureReading option)
  : BloodPressureReading option =
  let result = ref None

  let systolicField = makeField 0 (Dim.Absolute(5))
  let diastolicField = makeField 1 (Dim.Absolute(5))
  let heartRateField = makeField 2 (Dim.Absolute(5))
  let timestampField = makeField 3 (Dim.Absolute(16))
  let commentsField = makeField 4 (Dim.Fill(2))

  match existing with
  | Some reading ->
    systolicField.Text <- string reading.Systolic
    diastolicField.Text <- string reading.Diastolic
    heartRateField.Text <- string reading.HeartRate
    timestampField.Text <- reading.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
    commentsField.Text <- reading.Comments |> Option.defaultValue ""
  | None -> timestampField.Text <- DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm")

  let timestampHint =
    new Label(Text = "  (yyyy-MM-dd HH:mm)", X = Pos.Right(timestampField), Y = Pos.Absolute(3))

  let dialog =
    new Dialog(Title = title, Width = Dim.Percent(60), Height = Dim.Absolute(12))

  let saveButton = new Button(Text = "Save", IsDefault = true)

  saveButton.Accepting.Add(fun e ->
    let parsed =
      parseFields
        (string systolicField.Text)
        (string diastolicField.Text)
        (string heartRateField.Text)
        (string timestampField.Text)
        (string commentsField.Text)

    match parsed with
    | Error msg ->
      e.Handled <- true
      MessageBox.ErrorQuery(app, "Input Error", msg, "OK") |> ignore
    | Ok unvalidated ->
      match BloodPressureReading.parse ranges unvalidated with
      | Ok validated ->
        result.Value <-
          match existing with
          | Some r -> Some { validated with Id = r.Id }
          | None -> Some validated
      | Error errors ->
        e.Handled <- true

        MessageBox.ErrorQuery(app, "Validation Error", formatValidationErrors ranges errors, "OK")
        |> ignore)

  let cancelButton = new Button(Text = "Cancel")
  cancelButton.Accepting.Add(fun _ -> ())

  dialog.Add(
    makeLabel "Systolic:" 0,
    systolicField,
    makeLabel "Diastolic:" 1,
    diastolicField,
    makeLabel "Heart Rate:" 2,
    heartRateField,
    makeLabel "Timestamp:" 3,
    timestampField,
    timestampHint,
    makeLabel "Comments:" 4,
    commentsField
  )

  dialog.AddButton(saveButton)
  dialog.AddButton(cancelButton)

  app.Run(dialog) |> ignore
  result.Value

let private showEditDialog (app: IApplication) (ranges: ReadingRanges) (reading: BloodPressureReading) =
  showReadingDialog app ranges "Edit Reading" (Some reading)

let private showAddDialog (app: IApplication) (ranges: ReadingRanges) () =
  showReadingDialog app ranges "Add New Reading" None

[<EntryPoint>]
let main _ =
  let config =
    ConfigurationBuilder()
      .SetBasePath(AppContext.BaseDirectory)
      .AddJsonFile("appsettings.json", optional = false, reloadOnChange = false)
      .Build()

  let connectionString = config.GetConnectionString("DefaultConnection")
  let ranges = readRanges config

  use app = Application.Create()
  app.Init() |> ignore
  let repository = ReadingRepository.create connectionString

  let showChart readings =
    let path = Path.Combine(Path.GetTempPath(), "bpchart.html")
    File.WriteAllText(path, BpChart.toHtml readings)

    Process.Start(ProcessStartInfo("xdg-open", path, UseShellExecute = true))
    |> ignore

    MessageBox.Query(app, "Chart", "Switch to your default browser to view the chart.", "OK")
    |> ignore

  let showImportDialog () =
    let dialog =
      new OpenDialog(Title = "Import from Markdown", AllowsMultipleSelection = false)

    app.Run(dialog) |> ignore

    if dialog.Canceled then
      None
    else
      let path = string dialog.Path

      if String.IsNullOrEmpty(path) then
        None
      else
        let content = File.ReadAllText(path)
        let unvalidated = parseMarkdown content
        Some(import repository ranges unvalidated)

  use win =
    new BpMonitor.Tui.ReadingsWindow(
      app,
      repository,
      Some(fun () -> app.RequestStop()),
      Some(showAddDialog app ranges),
      Some(showEditDialog app ranges),
      Some showChart,
      Some showImportDialog
    )

  app.Run(win) |> ignore
  0
