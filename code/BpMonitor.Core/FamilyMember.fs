namespace BpMonitor.Core

open System

type FamilyMember =
  { Id: int
    Name: string
    IsAdmin: bool
    IsActive: bool
    PasswordHash: string option
    Goal: GoalRange
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }

type FamilyMemberError = | NameIsEmpty

module FamilyMember =
  let create (name: string) (isAdmin: bool) : Result<FamilyMember, FamilyMemberError> =
    if String.IsNullOrWhiteSpace(name) then
      Error NameIsEmpty
    else
      Ok
        { Id = 0
          Name = name.Trim()
          IsAdmin = isAdmin
          IsActive = true
          PasswordHash = None
          Goal = GoalRange.defaults
          CreatedAt = DateTimeOffset.MinValue
          ModifiedAt = DateTimeOffset.MinValue }

  /// Returns true when the list contains at least one member that is both admin and active.
  /// Used to enforce the invariant before saving member changes.
  let hasActiveAdmin (members: FamilyMember list) =
    members |> List.exists (fun m -> m.IsAdmin && m.IsActive)

  /// Returns true when the member has a password set (has claimed their account).
  let isClaimed (m: FamilyMember) = m.PasswordHash |> Option.isSome
