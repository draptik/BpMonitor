module BpMonitor.Pwa.Client.Main

open Elmish
open Bolero
open Bolero.Html
open Bolero.Templating.Client
open Microsoft.JSInterop
open BpMonitor.Pwa.Client.Reading
open BpMonitor.Pwa.Client.ReadingDto
open BpMonitor.Pwa.Client.Components

type Model = { Readings: Reading list }

let initModel = { Readings = [] }

type Message =
  | SaveReading of Reading
  | ReadingsLoaded of Reading list
  | Persisted
  | DbError of exn

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

let formatReading (r: Reading) =
  let ts = r.Timestamp.ToString("yyyy-MM-dd HH:mm")
  let comment = r.Comment |> Option.map (fun c -> $" ({c})") |> Option.defaultValue ""
  $"{ts} — {r.Systolic}/{r.Diastolic} bpm {r.HeartRate}{comment}"

let view model dispatch =
  concat {
    comp<EntryForm> { attr.callback "OnSave" (fun r -> dispatch (SaveReading r)) }

    ul {
      for r in model.Readings do
        li { formatReading r }
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

    Program.mkProgram init (update js) view
#if DEBUG
    |> Program.withHotReload
#endif
