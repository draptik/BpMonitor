module BpMonitor.TestSupport.TestBuilders

open System
open BpMonitor.Core

let mkReading id memberId sys dia hr (ts: DateTimeOffset) : BloodPressureReading =
  { Id = id
    MemberId = memberId
    Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = ts
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let mkMedication id memberId name (startDate: DateOnly) : Medication =
  { Id = id
    MemberId = memberId
    Name = name
    FullName = None
    Comment = None
    StartDate = startDate
    EndDate = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let mkMember id name isAdmin isActive : FamilyMember =
  { Id = id
    Name = name
    IsAdmin = isAdmin
    IsActive = isActive
    PasswordHash = None
    Goal = GoalRange.defaults
    Language = English
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }
