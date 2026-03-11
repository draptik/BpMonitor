namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module ReadingRepository =
  let create (connectionString: string) : IReadingRepository =
    let options =
      DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connectionString).Options

    let ctx = new BpMonitorDbContext(options)
    ctx.Database.EnsureCreated() |> ignore
    EfReadingRepository(ctx, System.TimeProvider.System)
