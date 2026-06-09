namespace BpMonitor.Data

open System

[<CLIMutable>]
type MemberRecord =
  { Id: int
    Name: string
    IsAdmin: bool
    IsActive: bool
    PasswordHash: string
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }
