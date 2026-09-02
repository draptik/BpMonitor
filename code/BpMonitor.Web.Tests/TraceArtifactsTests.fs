module TraceArtifactsTests

open BpMonitor.Web.E2E
open Xunit

type TraceArtifactsTests() =

  [<Fact>]
  member _.``sanitize strips characters unsafe for a filename``() =
    let sanitized =
      TraceArtifacts.sanitize
        "BpMonitor.Web.E2E.MedicationsScrubberTests.MedicationsScrubberTests.moving across a medication bar keeps boxing the matching value-strip column"

    Assert.DoesNotContain(" ", sanitized)
    Assert.DoesNotContain(",", sanitized)
    Assert.DoesNotContain("`", sanitized)
    Assert.DoesNotContain("'", sanitized)

  [<Fact>]
  member _.``sanitize produces different output for names differing only by punctuation``() =
    let a = TraceArtifacts.sanitize "reads a value's `x` coordinate"
    let b = TraceArtifacts.sanitize "reads a values x coordinate"

    Assert.NotEqual<string>(a, b)

  [<Fact>]
  member _.``sanitize keeps the result under a sane path length``() =
    let longName =
      String.replicate 20 "a very long test display name, with punctuation! "

    Assert.True(TraceArtifacts.sanitize(longName).Length <= 120)

  [<Fact>]
  member _.``pathFor joins the results directory and sanitized name with a trace extension``() =
    let path = TraceArtifacts.pathFor "/tmp/results" "some test`s name"

    Assert.StartsWith("/tmp/results", path)
    Assert.EndsWith(".zip", path)
