module MedicationHandlerTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

[<Fact>]
let ``settings renders the medications section with existing medications`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]
  TestHost.run ReadingHandlers.settings ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "HCTZ" @>
  test <@ body.Contains "hydrochlorothiazide" @>
  test <@ body.Contains "Medications" @>
  test <@ body.Contains "01.04.2026" @>

[<Fact>]
let ``create persists a valid medication and redirects to settings`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ"
      FormFields.medicationFullName, "hydrochlorothiazide"
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "2026-01-01"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.settings @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  let saved = medicationRepo.GetAll(defaultMemberId)
  test <@ saved.Length = 1 @>
  test <@ saved[0].Name = "HCTZ" @>
  test <@ saved[0].FullName = Some "hydrochlorothiazide" @>
  test <@ saved[0].EndDate = None @>

[<Fact>]
let ``create parses the start date as dd.mm.yyyy, not mm.dd.yyyy`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ"
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "01.02.2026"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 302 @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  let saved = medicationRepo.GetAll(defaultMemberId)
  test <@ saved[0].StartDate = DateOnly(2026, 2, 1) @>

[<Fact>]
let ``create accepts single-digit day and month`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ"
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "1.8.2026"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 302 @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  let saved = medicationRepo.GetAll(defaultMemberId)
  test <@ saved[0].StartDate = DateOnly(2026, 8, 1) @>

[<Fact>]
let ``create still accepts an iso-format start date`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ"
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "2026-01-01"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 302 @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  let saved = medicationRepo.GetAll(defaultMemberId)
  test <@ saved[0].StartDate = DateOnly(2026, 1, 1) @>

[<Fact>]
let ``create rejects an empty name with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, ""
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "2026-01-01"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "Name cannot be empty" @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  test <@ medicationRepo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``create rejects an end date before the start date with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ"
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "2026-01-10"
      FormFields.medicationEndDate, "2026-01-01" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "End date must be on or after the start date" @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  test <@ medicationRepo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``create rejects a non-parseable date with 422`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ"
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "not-a-date"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.create ctx

  test <@ ctx.Response.StatusCode = 422 @>

[<Fact>]
let ``edit prefills the form from the existing medication`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]
  TestHost.setRouteId ctx sampleMedication.Id

  TestHost.run MedicationHandlers.edit ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "value=\"HCTZ\"" @>
  test <@ body.Contains "value=\"hydrochlorothiazide\"" @>
  test <@ body.Contains "value=\"01.04.2026\"" @>
  test <@ body.Contains "dd.mm.yyyy" @>
  test <@ not (body.Contains "type=\"date\"") @>

[<Fact>]
let ``settings renders the add-medication date fields with a dd.mm.yyyy hint`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo []
  TestHost.run ReadingHandlers.settings ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "dd.mm.yyyy" @>
  test <@ not (body.Contains "type=\"date\"") @>

[<Fact>]
let ``edit returns 404 for a medication belonging to a different member`` () =
  let repo = repoWith []

  let ctx =
    TestHost.contextWithMedications repo [ { sampleMedication with MemberId = 999 } ]

  TestHost.setRouteId ctx sampleMedication.Id

  TestHost.run MedicationHandlers.edit ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``update persists changes and redirects to settings`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]
  TestHost.setRouteId ctx sampleMedication.Id

  TestHost.setForm
    ctx
    [ FormFields.medicationName, "HCTZ 25mg"
      FormFields.medicationFullName, "hydrochlorothiazide"
      FormFields.medicationComment, "Ran out"
      FormFields.medicationStartDate, "2026-04-01"
      FormFields.medicationEndDate, "2026-06-01" ]

  TestHost.run MedicationHandlers.update ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.settings @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  let updated = medicationRepo.GetAll(defaultMemberId) |> List.exactlyOne
  test <@ updated.Name = "HCTZ 25mg" @>
  test <@ updated.EndDate = Some(DateOnly(2026, 6, 1)) @>
  test <@ updated.Comment = Some "Ran out" @>

[<Fact>]
let ``update rejects an empty name with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]
  TestHost.setRouteId ctx sampleMedication.Id

  TestHost.setForm
    ctx
    [ FormFields.medicationName, ""
      FormFields.medicationFullName, ""
      FormFields.medicationComment, ""
      FormFields.medicationStartDate, "2026-04-01"
      FormFields.medicationEndDate, "" ]

  TestHost.run MedicationHandlers.update ctx

  test <@ ctx.Response.StatusCode = 422 @>
  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  test <@ (medicationRepo.GetAll(defaultMemberId) |> List.exactlyOne).Name = "HCTZ" @>

[<Fact>]
let ``update returns 404 for a medication belonging to a different member`` () =
  let repo = repoWith []

  let ctx =
    TestHost.contextWithMedications repo [ { sampleMedication with MemberId = 999 } ]

  TestHost.setRouteId ctx sampleMedication.Id
  TestHost.setForm ctx [ FormFields.medicationName, "x"; FormFields.medicationStartDate, "2026-01-01" ]

  TestHost.run MedicationHandlers.update ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``delete removes the medication and redirects to settings`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMedications repo [ sampleMedication ]
  TestHost.setRouteId ctx sampleMedication.Id

  TestHost.run MedicationHandlers.delete ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.settings @>

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  test <@ medicationRepo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``delete does not remove a medication belonging to a different member`` () =
  let repo = repoWith []
  let other = { sampleMedication with MemberId = 999 }
  let ctx = TestHost.contextWithMedications repo [ other ]
  TestHost.setRouteId ctx other.Id

  TestHost.run MedicationHandlers.delete ctx

  let medicationRepo = ctx.RequestServices.GetRequiredService<IMedicationRepository>()
  test <@ medicationRepo.GetAll(999).Length = 1 @>

// ── Medications Timeline panel on /recent and /history ─────────────────────────────

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
        EndDate = Some(DateOnly(2026, 7, 1)) }

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
        EndDate = Some(DateOnly(2020, 2, 1)) }

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
        EndDate = None }

  let repo = repoWith []
  let ctx = TestHost.contextWithMedicationsAndProvider repo [ ongoing ] tp

  TestHost.run ReadingHandlers.history ctx

  // The trace and axis both extend to "today" rather than stopping at the
  // medication's own StartDate or collapsing to "now" with no medications at all.
  test <@ (TestHost.readBody ctx).Contains "2026-06-15" @>
