module HandlerTestHelpers

open System
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

let repoWith readings : IReadingRepository =
  InMemoryReadingRepository(Some readings)
