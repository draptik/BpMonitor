/// Builds each production repository implementation by name, so contract tests can run
/// the same assertions against every one of them via [<InlineData>].
module RepositoryImplementations

open System
open BpMonitor.Core
open BpMonitor.Data

let private noop =
  { new IDisposable with
      member _.Dispose() = ()
  }

let readingRepo (impl: string) : IReadingRepository * IDisposable =
  match impl with
  | "InMemory" -> InMemoryReadingRepository(Some []) :> IReadingRepository, noop
  | "Ef" ->
    let ctx = EfTestContext.createContext ()
    EfReadingRepository(ctx, TimeProvider.System) :> IReadingRepository, ctx :> IDisposable
  | other -> failwith $"Unknown reading repository implementation: {other}"

let medicationRepo (impl: string) : IMedicationRepository * IDisposable =
  match impl with
  | "InMemory" -> InMemoryMedicationRepository(Some []) :> IMedicationRepository, noop
  | "Ef" ->
    let ctx = EfTestContext.createContext ()
    EfMedicationRepository(ctx, TimeProvider.System) :> IMedicationRepository, ctx :> IDisposable
  | other -> failwith $"Unknown medication repository implementation: {other}"
