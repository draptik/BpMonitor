module DemoSeederTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private ranges = ReadingRanges.defaults
let private now = DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero)

let private tp =
  new Microsoft.Extensions.Time.Testing.FakeTimeProvider(now) :> TimeProvider

let private emptyStore () =
  InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository,
  InMemoryReadingRepository(Some []) :> IReadingRepository

// ── disabled seeder ───────────────────────────────────────────────────────────

[<Fact>]
let ``disabled flag leaves the store untouched`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp false
  // Only the default "Me" member from InMemoryFamilyMemberRepository
  test <@ members.GetAll().Length = 1 @>
  test <@ (members.GetAll() |> List.head).Name = "Me" @>

// ── enabled seeder ────────────────────────────────────────────────────────────

[<Fact>]
let ``enabled seeder on empty store creates exactly 6 members`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp true
  test <@ members.GetAll().Length = 6 @>

[<Fact>]
let ``enabled seeder renames the default Me member (no extra member)`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp true
  let names = members.GetAll() |> List.map _.Name
  test <@ names |> List.contains "Me" |> not @>

[<Fact>]
let ``enabled seeder: Marge Simpson is the admin`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp true
  let admins = members.GetAll() |> List.filter _.IsAdmin
  test <@ admins.Length = 1 @>
  test <@ (admins |> List.head).Name = "Marge Simpson" @>

[<Fact>]
let ``enabled seeder: each member has readings scoped to their own Id`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp true

  for m in members.GetAll() do
    test <@ readings.GetAll(m.Id).Length > 0 @>

[<Fact>]
let ``enabled seeder: members are seeded unclaimed (no password)`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp true

  for m in members.GetAll() do
    test <@ m.PasswordHash = None @>

// ── idempotence ───────────────────────────────────────────────────────────────

[<Fact>]
let ``second call on populated store is a no-op`` () =
  let members, readings = emptyStore ()
  DemoSeeder.seedIfEmpty members readings ranges tp true
  let memberCountAfterFirst = members.GetAll().Length

  let totalAfterFirst =
    members.GetAll() |> List.sumBy (fun m -> readings.GetAll(m.Id).Length)

  DemoSeeder.seedIfEmpty members readings ranges tp true

  test <@ members.GetAll().Length = memberCountAfterFirst @>

  let totalAfterSecond =
    members.GetAll() |> List.sumBy (fun m -> readings.GetAll(m.Id).Length)

  test <@ totalAfterSecond = totalAfterFirst @>
