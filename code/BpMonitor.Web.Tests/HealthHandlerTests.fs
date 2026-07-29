module HealthHandlerTests

open System
open System.IO
open Xunit
open Swensen.Unquote
open BpMonitor.Web

[<Fact>]
let ``health returns 200 when the database is reachable`` () =
  let ctx = TestHost.healthContext $"Data Source={Path.GetTempFileName()}"
  TestHost.run HealthHandlers.health ctx
  test <@ ctx.Response.StatusCode = 200 @>

[<Fact>]
let ``health reports connected status and content type when the database is reachable`` () =
  let ctx = TestHost.healthContext $"Data Source={Path.GetTempFileName()}"
  TestHost.run HealthHandlers.health ctx
  let body = TestHost.readBody ctx
  test <@ ctx.Response.ContentType = "application/json; charset=utf-8" @>
  test <@ body.Contains("\"status\":\"healthy\"") @>
  test <@ body.Contains("\"database\":\"connected\"") @>
  test <@ body.Contains($"\"version\":\"{Version.current}\"") @>

[<Fact>]
let ``health returns 503 when the database is unreachable`` () =
  let ctx = TestHost.healthContext $"Data Source=/nonexistent-{Guid.NewGuid()}/x.db"
  TestHost.run HealthHandlers.health ctx
  let body = TestHost.readBody ctx
  test <@ ctx.Response.StatusCode = 503 @>
  test <@ body.Contains("\"status\":\"unhealthy\"") @>
  test <@ body.Contains("\"database\":\"unreachable\"") @>
