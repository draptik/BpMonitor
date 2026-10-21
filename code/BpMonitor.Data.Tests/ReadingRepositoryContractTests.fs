module ReadingRepositoryContractTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders

let private reading sys dia hr =
  mkReading 0 0 sys dia hr (Timestamp.utc 2026 1 1 9 0 0)

/// Adds `r` for `memberId` and returns it as stored, with its assigned Id.
let private seed (repo: IReadingRepository) memberId r =
  repo.Add memberId r
  repo.GetAll(memberId) |> List.maxBy _.Id

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update replaces the reading with the matching Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let stored = seed repo 1 (reading 120 80 70)

  repo.Update(
    { stored with
        Systolic = 135
        Comments = Some "updated"
    }
  )

  test <@ repo.GetAll(1) |> List.exists (fun r -> r.Systolic = 135) @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update does not affect other readings`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let first = seed repo 1 (reading 120 80 70)
  let second = seed repo 1 (reading 130 85 72)
  repo.Update({ first with Systolic = 135 })
  test <@ repo.GetAll(1) |> List.exists (fun r -> r.Id = second.Id && r.Systolic = 130) @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update preserves the Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let stored = seed repo 1 (reading 120 80 70)
  repo.Update({ stored with Systolic = 135 })
  test <@ repo.GetAll(1) |> List.forall (fun r -> r.Id = stored.Id) @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update reflects changes in subsequent GetAll`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let stored = seed repo 1 (reading 120 80 70)

  repo.Update(
    { stored with
        Comments = Some "after update"
    }
  )

  test <@ repo.GetAll(1).[0].Comments = Some "after update" @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``GetAll returns only readings for the requested member`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let r1 = seed repo 1 (reading 120 80 70)
  let r2 = seed repo 2 (reading 130 85 72)
  test <@ repo.GetAll(1) |> List.length = 1 @>
  test <@ repo.GetAll(1).[0].Id = r1.Id @>
  test <@ repo.GetAll(2).[0].Id = r2.Id @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Add stamps the reading with the given memberId`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  repo.Add 2 (reading 120 80 70)
  test <@ repo.GetAll(2) |> List.length = 1 @>
  test <@ repo.GetAll(1) |> List.isEmpty @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update does not affect a reading belonging to a different member`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let stored = seed repo 1 (reading 120 80 70)
  // Attempt to update the reading as if it belonged to member 2 — should be a no-op
  repo.Update(
    { stored with
        MemberId = 2
        Systolic = 135
    }
  )

  test <@ repo.GetAll(1) |> List.exists (fun x -> x.Systolic = 120) @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update of a non-existent reading is a no-op`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let stored = seed repo 1 (reading 120 80 70)
  repo.Update({ stored with Id = 999; Systolic = 135 })
  test <@ repo.GetAll(1) = [ stored ] @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``TryGet returns the member's reading with that Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  seed repo 1 (reading 120 80 70) |> ignore
  let stored = seed repo 1 (reading 130 85 72)
  test <@ repo.TryGet 1 stored.Id = Some stored @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``TryGet returns None for another member's reading`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  let stored = seed repo 2 (reading 120 80 70)
  test <@ repo.TryGet 1 stored.Id = None @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``TryGet returns None for an unknown Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.readingRepo impl
  use _ = scope
  seed repo 1 (reading 120 80 70) |> ignore
  test <@ repo.TryGet 1 999 = None @>
