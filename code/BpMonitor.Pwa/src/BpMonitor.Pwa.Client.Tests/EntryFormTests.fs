module EntryFormTests

open System
open Microsoft.AspNetCore.Components
open Bunit
open Xunit
open Swensen.Unquote
open BpMonitor.Pwa.Client.Components
open BpMonitor.Pwa.Client.Reading

let renderWithSave (ctx: BunitContext) (onSave: Reading -> unit) : IRenderedComponent<EntryForm> =
  ctx.Render<EntryForm>(
    Action<ComponentParameterCollectionBuilder<EntryForm>>(fun p ->
      p.Add((fun (c: EntryForm) -> c.OnSave), Action<Reading>(onSave)) |> ignore)
  )

[<Fact>]
let ``entry form calls onSave with reading when submitted`` () =
  use ctx = new BunitContext()
  let mutable saved: Reading option = None
  let cut = renderWithSave ctx (fun r -> saved <- Some r)
  cut.Instance.Systolic <- "120"
  cut.Instance.Diastolic <- "80"
  cut.Instance.HeartRate <- "65"
  cut.Instance.Submit() |> ignore
  test <@ saved |> Option.map (fun r -> r.Systolic, r.Diastolic, r.HeartRate) = Some(120, 80, 65) @>

[<Fact>]
let ``entry form calls onSave with timestamp and comment when submitted`` () =
  use ctx = new BunitContext()
  let mutable saved: Reading option = None
  let ts = DateTimeOffset(2026, 3, 16, 9, 0, 0, TimeSpan.Zero)
  let cut = renderWithSave ctx (fun r -> saved <- Some r)
  cut.Instance.Systolic <- "120"
  cut.Instance.Diastolic <- "80"
  cut.Instance.HeartRate <- "65"
  cut.Instance.Timestamp <- ts
  cut.Instance.Comment <- "after exercise"
  cut.Instance.Submit() |> ignore
  test <@ saved |> Option.map (fun r -> r.Timestamp, r.Comment) = Some(ts, Some "after exercise") @>
