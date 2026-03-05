namespace BpMonitor.Tui

open System
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open BpMonitor.Core

type DataEntryWindow(app: IApplication) as this =
    inherit Window()

    let makeLabel (text: string) (y: int) =
        new Label(Text = text, X = Pos.Absolute(0), Y = Pos.Absolute(y))

    let makeField (y: int) =
        new TextField(X = Pos.Absolute(20), Y = Pos.Absolute(y), Width = Dim.Fill())

    let systolicLabel  = makeLabel "Systolic:"   0
    let systolicField  = makeField 0
    let diastolicLabel = makeLabel "Diastolic:"  1
    let diastolicField = makeField 1
    let heartRateLabel = makeLabel "Heart Rate:" 2
    let heartRateField = makeField 2
    let timestampLabel = makeLabel "Timestamp:"  3
    let timestampField =
        let f = makeField 3
        f.Text <- string DateTimeOffset.UtcNow
        f
    let commentsLabel  = makeLabel "Comments:"   4
    let commentsField  = makeField 4
    let submitButton   = new Button(Text = "Submit", X = Pos.Absolute(0), Y = Pos.Absolute(6), IsDefault = true)

    let clearForm () =
        systolicField.Text  <- ""
        diastolicField.Text <- ""
        heartRateField.Text <- ""
        timestampField.Text <- string DateTimeOffset.UtcNow
        commentsField.Text  <- ""

    let tryParseInt (label: string) (s: string) =
        match Int32.TryParse(s) with
        | true, v -> Ok v
        | _       -> Error $"{label}: '{s}' is not a valid integer"

    let tryParseTimestamp (s: string) =
        match DateTimeOffset.TryParse(s) with
        | true, v -> Ok v
        | _       -> Error $"Timestamp: '{s}' is not a valid date/time"

    do
        this.Title <- "BpMonitor — New Reading"

        submitButton.Accepting.Add(fun _ ->
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
                | Ok _ ->
                    MessageBox.Query(app, "Saved", "Reading recorded successfully.", "OK") |> ignore
                    clearForm()
                | Error e ->
                    let msg =
                        match e with
                        | SystolicOutOfRange v  -> $"Systolic {v} is out of range (1–300)"
                        | DiastolicOutOfRange v -> $"Diastolic {v} is out of range (1–200)"
                        | HeartRateOutOfRange v -> $"Heart rate {v} is out of range (1–300)"
                    MessageBox.ErrorQuery(app, "Validation Error", msg, "OK") |> ignore
        )

        this.Add(
            systolicLabel,  systolicField,
            diastolicLabel, diastolicField,
            heartRateLabel, heartRateField,
            timestampLabel, timestampField,
            commentsLabel,  commentsField,
            submitButton
        )
