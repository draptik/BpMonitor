module FamilyMemberTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core

[<Fact>]
let ``create seeds default goal range`` () =
  match FamilyMember.create "Alice" true with
  | Error _ -> failwith "unexpected error"
  | Ok m -> test <@ m.Goal = GoalRange.defaults @>

[<Fact>]
let ``create with isAdmin=true yields admin active member`` () =
  match FamilyMember.create "Alice" true with
  | Error _ -> failwith "unexpected error"
  | Ok m ->
    test <@ m.IsAdmin = true @>
    test <@ m.IsActive = true @>

[<Fact>]
let ``create with isAdmin=false yields non-admin active member`` () =
  match FamilyMember.create "Bob" false with
  | Error _ -> failwith "unexpected error"
  | Ok m ->
    test <@ m.IsAdmin = false @>
    test <@ m.IsActive = true @>

[<Fact>]
let ``create trims name and keeps it`` () =
  match FamilyMember.create "  Alice  " false with
  | Error _ -> failwith "unexpected error"
  | Ok m -> test <@ m.Name = "Alice" @>

[<Fact>]
let ``create blank name returns NameIsEmpty`` () =
  test <@ FamilyMember.create "   " false = Error NameIsEmpty @>

[<Fact>]
let ``hasActiveAdmin is false for empty list`` () =
  test <@ FamilyMember.hasActiveAdmin [] = false @>

[<Fact>]
let ``hasActiveAdmin is false when no member is admin`` () =
  let members =
    [ { Id = 1
        Name = "A"
        IsAdmin = false
        IsActive = true
        PasswordHash = None
        Goal = GoalRange.defaults
        CreatedAt = System.DateTimeOffset.MinValue
        ModifiedAt = System.DateTimeOffset.MinValue } ]

  test <@ FamilyMember.hasActiveAdmin members = false @>

[<Fact>]
let ``hasActiveAdmin is false when admin member is inactive`` () =
  let members =
    [ { Id = 1
        Name = "A"
        IsAdmin = true
        IsActive = false
        PasswordHash = None
        Goal = GoalRange.defaults
        CreatedAt = System.DateTimeOffset.MinValue
        ModifiedAt = System.DateTimeOffset.MinValue } ]

  test <@ FamilyMember.hasActiveAdmin members = false @>

[<Fact>]
let ``hasActiveAdmin is true when at least one member is admin and active`` () =
  let members =
    [ { Id = 1
        Name = "A"
        IsAdmin = false
        IsActive = true
        PasswordHash = None
        Goal = GoalRange.defaults
        CreatedAt = System.DateTimeOffset.MinValue
        ModifiedAt = System.DateTimeOffset.MinValue }
      { Id = 2
        Name = "B"
        IsAdmin = true
        IsActive = true
        PasswordHash = None
        Goal = GoalRange.defaults
        CreatedAt = System.DateTimeOffset.MinValue
        ModifiedAt = System.DateTimeOffset.MinValue } ]

  test <@ FamilyMember.hasActiveAdmin members = true @>
