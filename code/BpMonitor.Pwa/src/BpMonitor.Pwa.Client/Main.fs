module BpMonitor.Pwa.Client.Main

open Elmish
open Bolero
open Bolero.Html
open Bolero.Templating.Client
open BpMonitor.Pwa.Client.Reading
open BpMonitor.Pwa.Client.Components

type Model = { Readings: Reading list }

let initModel = { Readings = [] }

type Message = SaveReading of Reading

let update message model =
  match message with
  | SaveReading r ->
    { model with
        Readings = model.Readings @ [ r ] }

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
    Program.mkSimple (fun _ -> initModel) update view
#if DEBUG
    |> Program.withHotReload
#endif
