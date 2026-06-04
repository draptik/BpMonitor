namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module ReadingRepository =
  let create (connectionString: string) : IReadingRepository =
    let options =
      DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connectionString).Options

    let ctx = new BpMonitorDbContext(options)
    SchemaMigrations.apply ctx
    EfReadingRepository(ctx, System.TimeProvider.System)

module FamilyMemberRepository =
  let create (connectionString: string) : IFamilyMemberRepository =
    let options =
      DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connectionString).Options

    let ctx = new BpMonitorDbContext(options)
    EfFamilyMemberRepository(ctx, System.TimeProvider.System)
