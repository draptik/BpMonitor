namespace BpMonitor.Data

open System
open BpMonitor.Core

/// Seeds the Simpson-family demo dataset into an empty store.
///
/// Intended for developer onboarding and demo runs only. Call once at startup,
/// gated behind the BpMonitor:SeedDemoData configuration flag (default: false).
module DemoSeeder =

  /// Returns true when the store is considered "empty":
  /// no readings exist for any of the currently known members.
  let private isEmpty (members: IFamilyMemberRepository) (readings: IReadingRepository) =
    members.GetAll() |> List.forall (fun m -> readings.GetAll(m.Id).IsEmpty)

  /// Seeds the Simpson family iff `enabled` is true and the store is empty.
  ///
  /// The lone auto-seeded "Me" member (created by SchemaMigrations) is repurposed
  /// as Marge Simpson so the final member count is exactly 5, not 6.
  let seedIfEmpty
    (members: IFamilyMemberRepository)
    (readings: IReadingRepository)
    (ranges: ReadingRanges)
    (timeProvider: TimeProvider)
    (enabled: bool)
    : unit =

    if not enabled then
      ()
    elif not (isEmpty members readings) then
      ()
    else
      let now = timeProvider.GetUtcNow()
      let simpsons = DemoData.simpsons ranges now

      // The auto-seeded default member list (usually just "Me", id=1).
      let existingMembers = members.GetAll()

      // Assign a created FamilyMember for each MemberSpec, reusing the lone
      // existing default member for the first Simpson (Marge) if there is one.
      simpsons
      |> List.iteri (fun i (spec, memberReadings) ->
        let member_ =
          match i, existingMembers with
          | 0, [ existing ] ->
            // Repurpose the lone existing "Me" member as Marge.
            let updated =
              { existing with
                  Name = spec.Name
                  IsAdmin = spec.IsAdmin
                  IsActive = true
                  PasswordHash = None
                  ModifiedAt = now }

            members.Update(updated)
            updated
          | _ ->
            match FamilyMember.create spec.Name spec.IsAdmin with
            | Ok m -> members.Add(m)
            | Error NameIsEmpty -> failwith $"DemoSeeder: could not create member '{spec.Name}'"

        readings.AddMany member_.Id memberReadings)
