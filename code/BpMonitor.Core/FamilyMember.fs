namespace BpMonitor.Core

open System

type FamilyMember =
  { Id: int
    Name: string
    IsAdmin: bool
    IsActive: bool
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
          CreatedAt = DateTimeOffset.MinValue
          ModifiedAt = DateTimeOffset.MinValue }

  /// Returns true when the list contains at least one member that is both admin and active.
  /// Used to enforce the invariant before saving member changes.
  let hasActiveAdmin (members: FamilyMember list) =
    members |> List.exists (fun m -> m.IsAdmin && m.IsActive)
