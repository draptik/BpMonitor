module MedicationHandlerTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
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
  test <@ body.Contains "value=\"2026-04-01\"" @>

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
