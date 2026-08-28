module RecentReadingsListTests

open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers
open RecentTestFixtures

[<Fact>]
let ``recent page renders a collapsible readings section below the chart citation`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ reading 1 1 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "<details class=\"collapsible recent-readings\" data-persist-key=\"recent-readings\">" @>

[<Fact>]
let ``recent readings section lists heart rate, comment and an edit link per reading`` () =
  let r =
    { reading 1 1 with
        HeartRate = 71
        Comments = Some "after walk" }

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "71" @>
  test <@ body.Contains "after walk" @>
  test <@ body.Contains(Routes.readingEdit r.Id) @>

[<Fact>]
let ``recent readings section tags each row with the reading's chart x-label`` () =
  let r1 = reading 3 1
  let r2 = reading 2 2

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r1; r2 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let expectedX1 = Formats.formatLocal r1.Timestamp
  let expectedX2 = Formats.formatLocal r2.Timestamp

  test <@ body.Contains $"<tr data-x=\"{expectedX1}\">" @>
  test <@ body.Contains $"<tr data-x=\"{expectedX2}\">" @>

[<Fact>]
let ``recent readings section marks a row older than 30 days out-of-range, like the value strip`` () =
  let tp = FakeTimeProvider(now)
  let oldReading = { reading 31 1 with Systolic = 130 }
  let recentReading = { reading 1 2 with Systolic = 125 }
  let ctx = TestHost.contextWithProvider (repoWith [ oldReading; recentReading ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let oldX = Formats.formatLocal oldReading.Timestamp
  let recentX = Formats.formatLocal recentReading.Timestamp

  test <@ body.Contains $"<tr data-x=\"{oldX}\" class=\"out-of-range\">" @>
  test <@ body.Contains $"<tr data-x=\"{recentX}\">" @>

[<Fact>]
let ``recent full fragment includes the readings section too`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ reading 1 1 ]) tp
  TestHost.run ReadingHandlers.recentFull ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "<details class=\"collapsible recent-readings\" data-persist-key=\"recent-readings\">" @>

[<Fact>]
let ``recent readings section lists rows newest first, like History`` () =
  let r1 = { reading 3 1 with Systolic = 130 }
  let r2 = { reading 2 2 with Systolic = 142 }
  let r3 = { reading 1 3 with Systolic = 118 }

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r1; r2; r3 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let sectionStart = body.IndexOf "recent-readings-table"
  let sectionEnd = body.IndexOf("</table>", sectionStart)
  let section = body.Substring(sectionStart, sectionEnd - sectionStart)

  test
    <@
      section.IndexOf ">118<" < section.IndexOf ">142<"
      && section.IndexOf ">142<" < section.IndexOf ">130<"
    @>

[<Fact>]
let ``recent readings section leaves a reading exactly 30 days old in range`` () =
  let tp = FakeTimeProvider(now)
  let atBoundary = { reading 30 1 with Systolic = 130 }
  let ctx = TestHost.contextWithProvider (repoWith [ atBoundary ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let x = Formats.formatLocal atBoundary.Timestamp
  test <@ body.Contains $"<tr data-x=\"{x}\">" @>
  test <@ not (body.Contains $"<tr data-x=\"{x}\" class=\"out-of-range\">") @>

[<Fact>]
let ``recent readings section marks a reading just past 30 days old out-of-range`` () =
  let tp = FakeTimeProvider(now)

  let pastBoundary =
    { reading 0 1 with
        Systolic = 130
        Timestamp = now.AddDays(-30.0).AddSeconds(-1.0) }

  let ctx = TestHost.contextWithProvider (repoWith [ pastBoundary ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let x = Formats.formatLocal pastBoundary.Timestamp
  test <@ body.Contains $"<tr data-x=\"{x}\" class=\"out-of-range\">" @>

[<Fact>]
let ``recent full fragment marks a reading beyond the load window out-of-range in the readings section too`` () =
  let tp = FakeTimeProvider(now)
  let beyondLoadWindow = { reading 400 1 with Systolic = 199 }
  let ctx = TestHost.contextWithProvider (repoWith [ beyondLoadWindow ]) tp
  TestHost.run ReadingHandlers.recentFull ctx

  let body = TestHost.readBody ctx
  let x = Formats.formatLocal beyondLoadWindow.Timestamp
  test <@ body.Contains $"<tr data-x=\"{x}\" class=\"out-of-range\">" @>
  test <@ body.Contains ">199<" @>

[<Fact>]
let ``recent readings section renders an empty table without error when there are no readings`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "recent-readings-table" @>
  test <@ not (body.Contains "<tr data-x") @>

[<Fact>]
let ``recent readings section HTML-encodes a reading's comment`` () =
  let r =
    { reading 1 1 with
        Comments = Some "<script>x</script>" }

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "<script>x</script>") @>
  test <@ body.Contains "&lt;script&gt;" @>
