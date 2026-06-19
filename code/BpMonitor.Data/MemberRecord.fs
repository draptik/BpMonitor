namespace BpMonitor.Data

open System

[<CLIMutable>]
type MemberRecord =
  { Id: int
    Name: string
    IsAdmin: bool
    IsActive: bool
    PasswordHash: string
    SystolicGoalMin: int
    SystolicGoalMax: int
    DiastolicGoalMin: int
    DiastolicGoalMax: int
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }
