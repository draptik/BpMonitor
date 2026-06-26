module ViewTestHelpers

open System
open BpMonitor.Core

let defaultMember: FamilyMember =
  { Id = 1
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    Goal = GoalRange.defaults
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let sample: BloodPressureReading =
  { Id = 7
    MemberId = 1
    Systolic = 123
    Diastolic = 81
    HeartRate = 67
    Timestamp = Timestamp.utc 2026 5 1 9 0 0
    Comments = Some "after walk"
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }
