namespace BpMonitor.Data

open System

[<CLIMutable>]
type MemberRecord =
  { Id: int
    Name: string
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }
