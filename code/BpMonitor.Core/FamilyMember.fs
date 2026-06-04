namespace BpMonitor.Core

open System

type FamilyMember =
  { Id: int
    Name: string
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }

type FamilyMemberError = | NameIsEmpty

module FamilyMember =
  let create (name: string) : Result<FamilyMember, FamilyMemberError> =
    if String.IsNullOrWhiteSpace(name) then
      Error NameIsEmpty
    else
      Ok
        { Id = 0
          Name = name.Trim()
          CreatedAt = DateTimeOffset.MinValue
          ModifiedAt = DateTimeOffset.MinValue }
