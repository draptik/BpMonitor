namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore

type BpMonitorDbContext(options: DbContextOptions<BpMonitorDbContext>) =
  inherit DbContext(options)

  member this.Readings = this.Set<ReadingRecord>()
