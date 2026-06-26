module FamilyMemberTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open TestBuilders

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
  let members = [ mkMember 1 "A" false true ]
  test <@ FamilyMember.hasActiveAdmin members = false @>

[<Fact>]
let ``hasActiveAdmin is false when admin member is inactive`` () =
  let members = [ mkMember 1 "A" true false ]
  test <@ FamilyMember.hasActiveAdmin members = false @>

[<Fact>]
let ``hasActiveAdmin is true when at least one member is admin and active`` () =
  let members = [ mkMember 1 "A" false true; mkMember 2 "B" true true ]
  test <@ FamilyMember.hasActiveAdmin members = true @>

[<Fact>]
let ``isClaimed returns false when PasswordHash is None`` () =
  match FamilyMember.create "Alice" true with
  | Error _ -> failwith "unexpected error"
  | Ok m -> test <@ FamilyMember.isClaimed m = false @>

[<Fact>]
let ``isClaimed returns true when PasswordHash is set`` () =
  match FamilyMember.create "Alice" true with
  | Error _ -> failwith "unexpected error"
  | Ok m ->
    let claimed = { m with PasswordHash = Some "hashed" }
    test <@ FamilyMember.isClaimed claimed = true @>
