/// Shared between RecentHandlerTests and RecentValueStripTests.
module RecentTestFixtures

open BpMonitor.Core
open HandlerTestHelpers

let now = Timestamp.utc 2026 6 17 12 0 0

let reading daysAgo (id: int) : BloodPressureReading =
  { Id = id
    MemberId = defaultMemberId
    Systolic = 120
    Diastolic = 80
    HeartRate = 66
    Timestamp = now.AddDays(-float daysAgo)
    Comments = None
    CreatedAt = now
    ModifiedAt = now }
