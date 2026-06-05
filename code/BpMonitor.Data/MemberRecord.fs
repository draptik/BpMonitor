namespace BpMonitor.Data

open System

[<CLIMutable>]
type MemberRecord =
  { Id: int
    Name: string
    IsAdmin: bool
    IsActive: bool
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }
