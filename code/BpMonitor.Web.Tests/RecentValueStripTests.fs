module RecentValueStripTests

open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers
open RecentTestFixtures

// Three readings used by value-strip ordering and table-layout tests.
let private stripR1 =
  { reading 3 1 with
      Systolic = 130
      Diastolic = 82 }

let private stripR2 =
  { reading 2 2 with
      Systolic = 142
      Diastolic = 91 }

let private stripR3 =
  { reading 1 3 with
      Systolic = 118
      Diastolic = 76 }

[<Fact>]
let ``recent loads a reading older than 30 days but marks its value-strip cell out-of-range`` () =
  // TODOs.md: value-strip stays focused on the last 30 days, but panning back must still reveal older readings.
  let tp = FakeTimeProvider(now)
  let oldReading = { reading 31 1 with Systolic = 130 }
  let recentReading = { reading 1 2 with Systolic = 125 }
  let ctx = TestHost.contextWithProvider (repoWith [ oldReading; recentReading ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx

  let cells =
    System.Text.RegularExpressions.Regex.Matches(body, "<td class=\"([^\"]*)\"[^>]*>(\\d+)</td>")
    |> Seq.map (fun m -> m.Groups[2].Value, m.Groups[1].Value)
    |> Map.ofSeq

  test <@ cells["130"].Contains "out-of-range" @>
  test <@ not (cells["125"].Contains "out-of-range") @>

[<Fact>]
let ``recent shows a sys/dias value strip listing every reading in the chart window, oldest first`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ stripR1; stripR2; stripR3 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value-strip" @>

  let systolicRow =
    body.Substring(body.IndexOf "Systolic", body.IndexOf "Diastolic" - body.IndexOf "Systolic")

  let diastolicRow = body.Substring(body.IndexOf "Diastolic")

  test
    <@
      systolicRow.IndexOf "130" < systolicRow.IndexOf "142"
      && systolicRow.IndexOf "142" < systolicRow.IndexOf "118"
    @>

  test
    <@
      diastolicRow.IndexOf "82" < diastolicRow.IndexOf "91"
      && diastolicRow.IndexOf "91" < diastolicRow.IndexOf "76"
    @>

[<Fact>]
let ``recent value strip uses a table so each reading's sys/dias values align in the same column`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ stripR1; stripR2; stripR3 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let stripStart = body.IndexOf "value-strip"
  let stripEnd = body.IndexOf("</table>", stripStart)
  let strip = body.Substring(stripStart, stripEnd - stripStart)

  let cellValues =
    System.Text.RegularExpressions.Regex.Matches(strip, "<td[^>]*>(\\d+)</td>")
    |> Seq.map _.Groups[1].Value
    |> List.ofSeq

  test <@ strip.Contains "<table" @>
  test <@ cellValues = [ "130"; "142"; "118"; "82"; "91"; "76" ] @>

[<Fact>]
let ``recent value strip tags each cell with the reading's chart x-label, for the scrubber bar to match against`` () =
  // Scrubber bar: the chart's hover payload reports Formats.formatLocal r.Timestamp, so each value-strip cell needs a matching data-x attribute.
  let r1 =
    { reading 3 1 with
        Systolic = 130
        Diastolic = 82 }

  let r2 =
    { reading 2 2 with
        Systolic = 142
        Diastolic = 91 }

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r1; r2 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let expectedX1 = Formats.formatLocal r1.Timestamp
  let expectedX2 = Formats.formatLocal r2.Timestamp

  test <@ body.Contains $"data-x=\"{expectedX1}\"" @>
  test <@ body.Contains $"data-x=\"{expectedX2}\"" @>

[<Fact>]
let ``recent page loads the scrubber script that keeps the value strip in sync with the x-axis`` () =
  // wwwroot/recent-scrubber.js (loaded globally by ViewLayout) keeps the value strip aligned with the chart's x-range on zoom/pan.
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ reading 1 1 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx

  test <@ body.Contains "/recent-scrubber.js" @>

[<Fact>]
let ``recent value strip marks a reading above the goal range as 'above'`` () =
  // Default goal range (GoalRange.defaults): systolic max 140. 142 > 140.
  let r = { reading 1 1 with Systolic = 142 }
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value-strip-value above" @>

[<Fact>]
let ``recent value strip marks a reading below the goal range as 'below'`` () =
  // Default goal range (GoalRange.defaults): diastolic min 60. 59 < 60.
  let r = { reading 1 1 with Diastolic = 59 }
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value-strip-value below" @>

[<Fact>]
let ``recent value strip leaves an in-range reading's cells unmarked`` () =
  // reading helper's defaults (Systolic = 120, Diastolic = 80) are inside GoalRange.defaults.
  let r = reading 1 1
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "value-strip-value above") @>
  test <@ not (body.Contains "value-strip-value below") @>
