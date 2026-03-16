namespace BpMonitor.Pwa.Client.Components

open System
open Microsoft.AspNetCore.Components
open Bolero
open Bolero.Html
open BpMonitor.Pwa.Client.Reading

type EntryForm() =
  inherit Component()

  [<Parameter>]
  member val OnSave: EventCallback<Reading> = EventCallback<Reading>() with get, set

  member val Systolic = "" with get, set
  member val Diastolic = "" with get, set
  member val HeartRate = "" with get, set
  member val Timestamp = DateTimeOffset.Now with get, set
  member val Comment = "" with get, set

  member this.Submit() =
    let comment =
      match this.Comment.Trim() with
      | "" -> None
      | s -> Some s

    let reading =
      { Systolic = int this.Systolic
        Diastolic = int this.Diastolic
        HeartRate = int this.HeartRate
        Timestamp = this.Timestamp
        Comment = comment }

    this.OnSave.InvokeAsync(reading)

  override this.Render() =
    concat {
      input {
        attr.name "systolic"
        bind.change.string this.Systolic (fun v -> this.Systolic <- v)
      }

      input {
        attr.name "diastolic"
        bind.change.string this.Diastolic (fun v -> this.Diastolic <- v)
      }

      input {
        attr.name "heartRate"
        bind.change.string this.HeartRate (fun v -> this.HeartRate <- v)
      }

      input {
        attr.name "comment"
        bind.change.string this.Comment (fun v -> this.Comment <- v)
      }

      button {
        attr.``type`` "button"
        on.task.click (fun _ -> this.Submit())
        "Save"
      }
    }
