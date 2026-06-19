module SettingsHandlerTests

open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

[<Fact>]
let ``settings renders the authenticated member's current goal range`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.settings ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "value=\"90\"" @>
  test <@ body.Contains "value=\"140\"" @>
  test <@ body.Contains "value=\"60\"" @>

[<Fact>]
let ``updateSettings persists a valid goal range and redirects to history`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [ "SystolicGoalMin", "100"
      "SystolicGoalMax", "130"
      "DiastolicGoalMin", "65"
      "DiastolicGoalMax", "85" ]

  TestHost.run ReadingHandlers.updateSettings ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/history" @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let updated = (memberRepo.GetById defaultMemberId).Value

  let expected: GoalRange =
    { SystolicMin = 100
      SystolicMax = 130
      DiastolicMin = 65
      DiastolicMax = 85 }

  test <@ updated.Goal = expected @>

/// A member with a non-default goal, so "unchanged" assertions below can't
/// trivially pass by coincidence with the seeded default (GoalRange.defaults).
let private memberWithCustomGoal: FamilyMember =
  let customGoal: GoalRange =
    { SystolicMin = 100
      SystolicMax = 130
      DiastolicMin = 65
      DiastolicMax = 85 }

  FamilyMember.create "Me" true
  |> Result.defaultWith (fun _ -> failwith "invalid member")
  |> fun m ->
      { m with
          Id = defaultMemberId
          Goal = customGoal }

[<Fact>]
let ``updateSettings rejects a systolic min greater than or equal to max with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ memberWithCustomGoal ]

  TestHost.setForm
    ctx
    [ "SystolicGoalMin", "140"
      "SystolicGoalMax", "100"
      "DiastolicGoalMin", "65"
      "DiastolicGoalMax", "85" ]

  TestHost.run ReadingHandlers.updateSettings ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "Systolic min must be less than systolic max" @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let unchanged = (memberRepo.GetById defaultMemberId).Value
  test <@ unchanged.Goal = memberWithCustomGoal.Goal @>

[<Fact>]
let ``updateSettings redisplays the submitted values, not the stale persisted goal, on validation failure`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [ "SystolicGoalMin", "140"
      "SystolicGoalMax", "100"
      "DiastolicGoalMin", "65"
      "DiastolicGoalMax", "85" ]

  TestHost.run ReadingHandlers.updateSettings ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value=\"140\"" @>
  test <@ body.Contains "value=\"100\"" @>

[<Fact>]
let ``updateSettings rejects non-numeric input with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ memberWithCustomGoal ]

  TestHost.setForm
    ctx
    [ "SystolicGoalMin", "abc"
      "SystolicGoalMax", "130"
      "DiastolicGoalMin", "65"
      "DiastolicGoalMax", "85" ]

  TestHost.run ReadingHandlers.updateSettings ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "Systolic min: &#39;abc&#39; is not a valid integer" @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let unchanged = (memberRepo.GetById defaultMemberId).Value
  test <@ unchanged.Goal = memberWithCustomGoal.Goal @>

[<Fact>]
let ``updateSettings accumulates an error for every invalid field, not just the first`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ memberWithCustomGoal ]

  TestHost.setForm
    ctx
    [ "SystolicGoalMin", "abc"
      "SystolicGoalMax", "130"
      "DiastolicGoalMin", "xyz"
      "DiastolicGoalMax", "85" ]

  TestHost.run ReadingHandlers.updateSettings ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Systolic min: &#39;abc&#39; is not a valid integer" @>
  test <@ body.Contains "Diastolic min: &#39;xyz&#39; is not a valid integer" @>
