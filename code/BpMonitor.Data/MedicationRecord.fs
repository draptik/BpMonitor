namespace BpMonitor.Data

open System

[<CLIMutable>]
type MedicationRecord =
  { Id: int
    MemberId: int
    Name: string
    FullName: string // null represents absent
    Comment: string // null represents absent
    StartDate: DateOnly
    EndDate: Nullable<DateOnly>
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }
