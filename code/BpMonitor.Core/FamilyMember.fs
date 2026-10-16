namespace BpMonitor.Core

open System

/// How a member signs in (see ADR 0012).
type Credential =
  /// Claimed: signs in with name and password.
  | Password of passwordHash: string
  /// Unclaimed: claiming requires the one-time setup code.
  | PendingSetup of setupCodeHash: string
  /// Unclaimed, no setup code issued yet (fresh install, upgrade, demo data).
  | NoCredential

type FamilyMember =
  {
    Id: int
    Name: string
    IsAdmin: bool
    IsActive: bool
    Credential: Credential
    Goal: GoalRange
    Language: Language
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset
  }

type FamilyMemberError = | NameIsEmpty

module FamilyMember =
  let create (name: string) (isAdmin: bool) : Result<FamilyMember, FamilyMemberError> =
    if String.IsNullOrWhiteSpace(name) then
      Error NameIsEmpty
    else
      Ok
        {
          Id = 0
          Name = name.Trim()
          IsAdmin = isAdmin
          IsActive = true
          Credential = NoCredential
          Goal = GoalRange.defaults
          Language = Language.defaultLanguage
          CreatedAt = DateTimeOffset.MinValue
          ModifiedAt = DateTimeOffset.MinValue
        }

  /// Returns true when the list contains at least one member that is both admin and active.
  /// Used to enforce the invariant before saving member changes.
  let hasActiveAdmin (members: FamilyMember list) =
    members |> List.exists (fun m -> m.IsAdmin && m.IsActive)

  /// Returns true when a different member (by Id) already uses the candidate's name,
  /// ignoring case and surrounding spaces — login looks members up the same way.
  let nameTaken (members: FamilyMember list) (candidate: FamilyMember) =
    members
    |> List.exists (fun m ->
      m.Id <> candidate.Id
      && m.Name.Trim().Equals(candidate.Name.Trim(), StringComparison.OrdinalIgnoreCase))

  /// A short fingerprint of the credential for the session cookie; it changes on every
  /// password reset or new password (the hash is salted), which revokes older sessions.
  let credentialStamp (credential: Credential) : string =
    let source =
      match credential with
      | Password hash -> $"password:{hash}"
      | PendingSetup hash -> $"setup:{hash}"
      | NoCredential -> "none"

    let digest =
      System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes source)

    Convert.ToBase64String(digest, 0, 16)

  /// Returns true when the member has a password set (has claimed their account).
  let isClaimed (m: FamilyMember) =
    match m.Credential with
    | Password _ -> true
    | PendingSetup _
    | NoCredential -> false
