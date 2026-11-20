namespace BpMonitor.Data

open System
open System.Linq
open System.Linq.Expressions
open Microsoft.EntityFrameworkCore

/// The guarded update the EF repositories share.
type internal EfUpdate =
  /// Replaces the row `isTarget` matches with `entity`; a no-op when no row matches.
  /// Detaches a tracked copy with the same key first, or EF rejects the second instance.
  static member ReplaceIfExists
    (ctx: DbContext, rows: DbSet<'T>, isTarget: Expression<Func<'T, bool>>, keyOf: 'T -> int, entity: 'T)
    : unit =
    if rows.AsNoTracking().Any(isTarget) then
      ctx.ChangeTracker.Entries<'T>()
      |> Seq.tryFind (fun e -> keyOf e.Entity = keyOf entity)
      |> Option.iter (fun e -> e.State <- EntityState.Detached)

      rows.Update(entity) |> ignore
      ctx.SaveChanges() |> ignore
