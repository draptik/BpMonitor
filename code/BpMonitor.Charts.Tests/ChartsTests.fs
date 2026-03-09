module ChartsTests

open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Xunit
open VerifyXunit
open BpMonitor.Core
open BpMonitor.Charts


let private readings = [
    { Id = 1; Systolic = 120; Diastolic = 80; HeartRate = 70
      Timestamp = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
      Comments = None }
    { Id = 2; Systolic = 135; Diastolic = 88; HeartRate = 78
      Timestamp = DateTimeOffset(2026, 1, 2, 8, 0, 0, TimeSpan.Zero)
      Comments = Some "After coffee" }
]

type ChartTests() =
    [<Fact>]
    member _.``toHtml matches snapshot``() : Task =
        let html = BpChart.toHtml readings
        let settings = VerifyTests.VerifySettings()
        settings.ScrubInlineGuids()
        settings.AddScrubber(fun sb ->
            let scrubbed = Regex.Replace(string sb, @"renderPlotly_[0-9a-f]{32}", "renderPlotly_GUID")
            sb.Clear().Append(scrubbed) |> ignore)
        Verifier.Verify(html, settings).ToTask()
