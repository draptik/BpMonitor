module HandlerTestHelpers

open System
open Xunit
open BpMonitor.Core
open BpMonitor.Data

let defaultMemberId = 1

let sample: BloodPressureReading =
  { Id = 1
    MemberId = defaultMemberId
    Systolic = 120
    Diastolic = 80
    HeartRate = 66
    Timestamp = Timestamp.utc 2026 5 1 9 0 0
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

/// A default admin member with Id=defaultMemberId. Mirrors the member pre-set in TestHost.context.
let sampleMember: FamilyMember =
  { Id = defaultMemberId
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    Goal = GoalRange.defaults
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

/// Creates an active admin member with a custom goal range for chart-assertion tests.
let memberWithGoal (goal: GoalRange) : FamilyMember = { sampleMember with Goal = goal }

/// Asserts that the goal-band y0/y1 values for the given range all appear in the HTML body.
let assertGoalBands (goal: GoalRange) (body: string) =
  Assert.Contains($"\"y0\":{goal.SystolicMin}", body)
  Assert.Contains($"\"y1\":{goal.SystolicMax}", body)
  Assert.Contains($"\"y0\":{goal.DiastolicMin}", body)
  Assert.Contains($"\"y1\":{goal.DiastolicMax}", body)

let repoWith readings : IReadingRepository =
  InMemoryReadingRepository(Some readings)
