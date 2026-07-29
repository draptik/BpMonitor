namespace BpMonitor.Web

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging

/// Anonymous liveness + database-reachability probe for container orchestrators.
module HealthHandlers =
  type HealthReport =
    { Status: string
      Version: string
      Database: string }

  /// 200 when the database can be opened, 503 otherwise — so a broken /data
  /// volume surfaces as an unhealthy container rather than a process that
  /// answers HTTP but cannot serve readings.
  let health: HttpContext -> Task =
    fun ctx ->
      let connected =
        try
          (HandlerHelpers.dbContext ctx).Database.CanConnect()
        with ex ->
          (HandlerHelpers.logger ctx).LogWarning(ex, "Health check: database unreachable")
          false

      let report =
        { Status = (if connected then "healthy" else "unhealthy")
          Version = Version.current
          Database = (if connected then "connected" else "unreachable") }

      HandlerHelpers.jsonResponse (if connected then 200 else 503) report ctx
