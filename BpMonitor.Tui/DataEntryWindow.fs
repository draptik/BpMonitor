namespace BpMonitor.Tui

open System
open System.Collections.Generic
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open BpMonitor.Core

type DataEntryWindow(app: IApplication, repository: IReadingRepository, onQuit: (unit -> unit) option, onAdd: (unit -> BloodPressureReading option) option, onEdit: (BloodPressureReading -> BloodPressureReading option) option) as this =
    inherit Window()

    let makeTableSource () =
        EnumerableTableSource<BloodPressureReading>(
            repository.GetAll(),
            Dictionary<string, Func<BloodPressureReading, obj>>(
                dict
                    [ " Timestamp",
                      Func<BloodPressureReading, obj>(fun r ->
                          box (r.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm")))
                      "Sys ", Func<BloodPressureReading, obj>(fun r -> box r.Systolic)
                      "Dia ", Func<BloodPressureReading, obj>(fun r -> box r.Diastolic)
                      "HR ",  Func<BloodPressureReading, obj>(fun r -> box r.HeartRate)
                      " Comments", Func<BloodPressureReading, obj>(fun r -> box (r.Comments |> Option.defaultValue "")) ]
            )
        )

    let tableView =
        let tv =
            new TableView(
                X = Pos.Absolute(0),
                Y = Pos.Absolute(0),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Table = makeTableSource ()
            )

        tv.Style.ShowHorizontalBottomLine <- true
        let left  (v: obj) = " " + string v
        let right (v: obj) = string v + " "

        tv.Style.ColumnStyles[0] <- ColumnStyle(Alignment = Alignment.Start, MinWidth = 20, RepresentationGetter = Func<obj, string>(left))
        tv.Style.ColumnStyles[1] <- ColumnStyle(Alignment = Alignment.End, MinWidth = 5, RepresentationGetter = Func<obj, string>(right))
        tv.Style.ColumnStyles[2] <- ColumnStyle(Alignment = Alignment.End, MinWidth = 5, RepresentationGetter = Func<obj, string>(right))
        tv.Style.ColumnStyles[3] <- ColumnStyle(Alignment = Alignment.End, MinWidth = 5, RepresentationGetter = Func<obj, string>(right))
        tv.Style.ColumnStyles[4] <- ColumnStyle(Alignment = Alignment.Start, RepresentationGetter = Func<obj, string>(left))
        tv

    do
        this.Title <- "My Blood Pressure"

        if not (obj.ReferenceEquals(app, null)) then
            app.Keyboard.KeyDown.Add(fun key ->
                if key = Key.A then this.AddNew()
                elif key = Key.E then this.EditSelected())

        let statusBar =
            new StatusBar(
                [| new Shortcut(Key.A,   "Add",  (fun () -> ()))
                   new Shortcut(Key.E,   "Edit", (fun () -> ()))
                   new Shortcut(Key.Esc, "Quit", (fun () -> onQuit |> Option.iter (fun f -> f ()))) |])

        this.Add(tableView, statusBar)

    member _.Readings = repository.GetAll()

    member _.AddNew() =
        onAdd |> Option.iter (fun f ->
            match f () with
            | Some reading ->
                repository.Add(reading)
                tableView.Table <- makeTableSource()
            | None -> ())

    member _.EditSelected() =
        let readings = repository.GetAll()
        if readings.Length > 0 then
            let selected = readings |> List.item tableView.SelectedRow
            onEdit |> Option.iter (fun f ->
                match f selected with
                | Some updated ->
                    repository.Update(updated)
                    tableView.Table <- makeTableSource()
                | None -> ())
