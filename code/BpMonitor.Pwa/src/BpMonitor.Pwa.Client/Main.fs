module BpMonitor.Pwa.Client.Main

open System.Text.Json
open Elmish
open Bolero
open Bolero.Html
open Bolero.Templating.Client
open Microsoft.JSInterop
open BpMonitor.Pwa.Client.Reading
open BpMonitor.Pwa.Client.ReadingDto
open BpMonitor.Pwa.Client.Components

type SyncStatus =
  | Idle
  | SyncDone
  | SyncError of string

type Model =
  { Readings: Reading list
    SyncStatus: SyncStatus }

let initModel = { Readings = []; SyncStatus = Idle }

type Message =
  | SaveReading of Reading
  | ReadingsLoaded of Reading list
  | Persisted
  | DbError of exn
  | PushDone
  | PullDone of Reading list
  | SyncFailed of string

let update (js: IJSRuntime) message model =
  match message with
  | SaveReading r ->
    let cmd =
      Cmd.OfTask.either
        (fun () -> task { do! js.InvokeVoidAsync("bpMonitor.saveReading", toDto r).AsTask() })
        ()
        (fun () -> Persisted)
        DbError

    { model with
        Readings = model.Readings @ [ r ] },
    cmd
  | ReadingsLoaded readings -> { model with Readings = readings }, Cmd.none
  | Persisted
  | DbError _ -> model, Cmd.none
  | PushDone -> { model with SyncStatus = SyncDone }, Cmd.none
  | PullDone readings ->
    { model with
        Readings = readings
        SyncStatus = SyncDone },
    Cmd.none
  | SyncFailed msg ->
    { model with
        SyncStatus = SyncError msg },
    Cmd.none

let formatReading (r: Reading) =
  let ts = r.Timestamp.ToString("yyyy-MM-dd HH:mm")
  let comment = r.Comment |> Option.map (fun c -> $" ({c})") |> Option.defaultValue ""
  $"{ts} — {r.Systolic}/{r.Diastolic} bpm {r.HeartRate}{comment}"

let view (js: IJSRuntime) model dispatch =
  concat {
    comp<EntryForm> { attr.callback "OnSave" (fun r -> dispatch (SaveReading r)) }

    ul {
      for r in model.Readings do
        li { formatReading r }
    }

    div {
      h3 { "Nextcloud sync" }

      button {
        attr.``type`` "button"

        on.task.click (fun _ ->
          task {
            try
              let json =
                JsonSerializer.Serialize(model.Readings |> List.map toDto |> List.toArray)

              do! js.InvokeVoidAsync("bpMonitor.writeReadings", json).AsTask()
              dispatch PushDone
            with ex ->
              dispatch (SyncFailed ex.Message)
          })

        "Push to Nextcloud"
      }

      button {
        attr.``type`` "button"

        on.task.click (fun _ ->
          task {
            try
              let! json = js.InvokeAsync<string>("bpMonitor.readFileReadings").AsTask()

              if isNull json then
                dispatch (SyncFailed "readings.json not found in selected folder")
              else
                let dtos = JsonSerializer.Deserialize<ReadingDto[]>(json)

                let readings =
                  if isNull dtos then
                    []
                  else
                    dtos |> Array.map fromDto |> Array.toList

                do! js.InvokeVoidAsync("bpMonitor.clearReadings").AsTask()

                for r in readings do
                  do! js.InvokeVoidAsync("bpMonitor.saveReading", toDto r).AsTask()

                dispatch (PullDone readings)
            with ex ->
              dispatch (SyncFailed ex.Message)
          })

        "Pull from Nextcloud"
      }

      match model.SyncStatus with
      | Idle -> ()
      | SyncDone -> text "Done"
      | SyncError msg -> text $"Error: {msg}"
    }
  }

type MyApp() =
  inherit ProgramComponent<Model, Message>()

  override this.Program =
    let js = this.JSRuntime

    let init _ =
      initModel,
      Cmd.OfTask.either
        (fun () ->
          task {
            let! dtos = js.InvokeAsync<ReadingDto[]>("bpMonitor.loadReadings").AsTask()
            return dtos |> Array.map fromDto |> Array.toList
          })
        ()
        ReadingsLoaded
        DbError

    Program.mkProgram init (update js) (view js)
#if DEBUG
    |> Program.withHotReload
#endif
