namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design

type BpMonitorDbContextFactory() =
  interface IDesignTimeDbContextFactory<BpMonitorDbContext> with
    member _.CreateDbContext(_args: string[]) =
      let options =
        DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite("Data Source=bpmonitor.db").Options

      new BpMonitorDbContext(options)
