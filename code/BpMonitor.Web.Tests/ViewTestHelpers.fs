module ViewTestHelpers

open System
open BpMonitor.Core

let s: LocalizedStrings = LocalizedStrings.en

let defaultMember: FamilyMember = TestHost.defaultMember

/// Distinct from HandlerTestHelpers.sample: Id=7 and its Comments text are asserted
/// on directly in view-rendering tests (e.g. HistoryViewTests' edit-form/route checks).
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
