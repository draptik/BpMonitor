/// Medications Timeline panel on /recent, /history and /trends — a ReadingHandlers feature, not MedicationHandlers.
module MedicationsTimelineHandlerTests

open System
open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

[<Fact>]
let ``recent renders the Medications Timeline panel when the member has medications`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]

  TestHost.run ReadingHandlers.recent ctx

  test <@ (TestHost.readBody ctx).Contains "Medications Timeline" @>

[<Fact>]
let ``recent omits the Medications Timeline panel when the member has no medications`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.contextWithMedications repo []

  TestHost.run ReadingHandlers.recent ctx

  test <@ (TestHost.readBody ctx).Contains "Medications Timeline" |> not @>

[<Fact>]
let ``history renders the Medications Timeline panel when the member has medications`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]

  TestHost.run ReadingHandlers.history ctx

  test <@ (TestHost.readBody ctx).Contains "Medications Timeline" @>

[<Fact>]
let ``trends does not render the Medications Timeline panel`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]

  TestHost.run ReadingHandlers.trends ctx

  test <@ (TestHost.readBody ctx).Contains "Medications Timeline" |> not @>

[<Fact>]
let ``history renders a medication whose entire date span falls after the last reading`` () =
  // `sample`'s reading is 2026-05-01; a medication starting well after that would be
  // dropped if the timeline's window were derived from the readings alone.
  let laterMedication =
    { sampleMedication with
        StartDate = DateOnly(2026, 6, 1)
        EndDate = Some(DateOnly(2026, 7, 1))
    }

  let repo = repoWith [ sample ]
  let ctx = TestHost.contextWithMedications repo [ laterMedication ]

  TestHost.run ReadingHandlers.history ctx

  test <@ (TestHost.readBody ctx).Contains "Medications Timeline" @>

[<Fact>]
let ``history renders a medication when the member has no readings at all`` () =
  // With no readings, a window derived solely from them collapses to "now" — a
  // medication with a fixed past date span would then fall outside it.
  let pastMedication =
    { sampleMedication with
        StartDate = DateOnly(2020, 1, 1)
        EndDate = Some(DateOnly(2020, 2, 1))
    }

  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo [ pastMedication ]

  TestHost.run ReadingHandlers.history ctx

  test <@ (TestHost.readBody ctx).Contains "Medications Timeline" @>

[<Fact>]
let ``history extends an ongoing medication's timeline span to today`` () =
  // EndDate = None means "still ongoing" — the span's high end should be today
  // (per the time provider), not e.g. the medication's own StartDate.
  let today = DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero)
  let tp = FakeTimeProvider(today)

  let ongoing =
    { sampleMedication with
        StartDate = DateOnly(2026, 6, 1)
        EndDate = None
    }

  let repo = repoWith []
  let ctx = TestHost.contextWithMedicationsAndProvider repo [ ongoing ] tp

  TestHost.run ReadingHandlers.history ctx

  // The trace and axis both extend to "today" rather than stopping at the
  // medication's own StartDate or collapsing to "now" with no medications at all.
  test <@ (TestHost.readBody ctx).Contains "2026-06-15" @>

// /recent loads a year of readings behind a 30-day view; panning the BP chart back
// pans the timeline too (medications-sync.js), so it needs those medications as well.
let private recentNow = DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero)

let private springMedication =
  { sampleMedication with
      Name = "Ramipril"
      StartDate = DateOnly(2026, 1, 1)
      EndDate = Some(DateOnly(2026, 3, 31))
  }

[<Fact>]
let ``recent includes a medication from earlier in the loaded year, outside the 30-day view`` () =
  let repo =
    repoWith
      [
        { sample with
            Timestamp = Timestamp.utc 2026 2 10 8 0 0
        }
      ]

  let ctx =
    TestHost.contextWithMedicationsAndProvider repo [ springMedication ] (FakeTimeProvider recentNow)

  TestHost.run ReadingHandlers.recent ctx

  test <@ (TestHost.readBody ctx).Contains "\"ticktext\":[\"Ramipril" @>

[<Fact>]
let ``recentFull includes a medication from before the default one-year window`` () =
  let oldMedication =
    { springMedication with
        StartDate = DateOnly(2024, 1, 1)
        EndDate = Some(DateOnly(2024, 3, 31))
    }

  let repo =
    repoWith
      [
        { sample with
            Timestamp = Timestamp.utc 2024 2 10 8 0 0
        }
      ]

  let ctx =
    TestHost.contextWithMedicationsAndProvider repo [ oldMedication ] (FakeTimeProvider recentNow)

  TestHost.run ReadingHandlers.recentFull ctx

  test <@ (TestHost.readBody ctx).Contains "\"ticktext\":[\"Ramipril" @>

[<Fact>]
let ``recent still opens the Medications Timeline on the last 30 days`` () =
  let repo =
    repoWith
      [
        { sample with
            Timestamp = Timestamp.utc 2026 2 10 8 0 0
        }
      ]

  let ctx =
    TestHost.contextWithMedicationsAndProvider repo [ springMedication ] (FakeTimeProvider recentNow)

  TestHost.run ReadingHandlers.recent ctx

  // Once for the BP chart, once for the timeline below it.
  let windowStart = Formats.formatLocal (recentNow.AddDays -30.0)
  let marker = $"\"range\":[\"{windowStart}\""
  let body = TestHost.readBody ctx
  test <@ body.Split(marker).Length - 1 = 2 @>

[<Fact>]
let ``history ends the timeline's initial range at the newest reading, not at midnight that day`` () =
  let evening = DateTimeOffset(2026, 6, 15, 20, 0, 0, TimeSpan.Zero)

  let earlierMedication =
    { sampleMedication with
        StartDate = DateOnly(2026, 6, 1)
        EndDate = Some(DateOnly(2026, 6, 10))
    }

  let repo = repoWith [ { sample with Timestamp = evening } ]

  let ctx =
    TestHost.contextWithMedicationsAndProvider repo [ earlierMedication ] (FakeTimeProvider(evening.AddHours 1.0))

  TestHost.run ReadingHandlers.history ctx

  // The full range pattern: the reading's time alone also appears in the BP chart's data.
  let pattern =
    $"\"range\":\\[\"[^\"]+\",\"{Regex.Escape(Formats.formatLocal evening)}\"\\]"

  test <@ Regex.IsMatch(TestHost.readBody ctx, pattern) @>
