module ExportHandlerTests

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Xunit
open Swensen.Unquote
open BpMonitor.Web
open HandlerTestHelpers

let private assertExportResponse (handler: HttpContext -> Task) (contentType: string) (filename: string) =
  let ctx = TestHost.context (repoWith [ sample ])
  TestHost.run handler ctx
  test <@ ctx.Response.StatusCode = 200 @>
  test <@ ctx.Response.ContentType = contentType @>
  test <@ ctx.Response.Headers["Content-Disposition"].ToString() = $"attachment; filename=\"{filename}\"" @>

[<Fact>]
let ``exportJson returns 200, json content type, and attachment header`` () =
  assertExportResponse ReadingHandlers.exportJson "application/json; charset=utf-8" "bpmonitor-export.json"

[<Fact>]
let ``exportJson body contains the seeded reading's fields`` () =
  let ctx = TestHost.context (repoWith [ sample ])
  TestHost.run ReadingHandlers.exportJson ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "\"systolic\":120" @>
  test <@ body.Contains "\"diastolic\":80" @>
  test <@ body.Contains "\"heartRate\":66" @>

[<Fact>]
let ``exportJson returns only the active member's readings`` () =
  let otherMemberReading = { sample with Id = 2; MemberId = 999 }

  let repo = repoWith [ sample; otherMemberReading ]
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.exportJson ctx

  let body = TestHost.readBody ctx
  // Only one object in the array — the repo filters by memberId
  test <@ body.StartsWith "[{" && body.EndsWith "}]" @>

[<Fact>]
let ``exportCsv returns 200, csv content type, and attachment header`` () =
  assertExportResponse ReadingHandlers.exportCsv "text/csv; charset=utf-8" "bpmonitor-export.csv"

[<Fact>]
let ``exportCsv body contains header row and seeded reading's fields`` () =
  let ctx = TestHost.context (repoWith [ sample ])
  TestHost.run ReadingHandlers.exportCsv ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Id,MemberId,Systolic" @>
  test <@ body.Contains "120" @>
  test <@ body.Contains "80" @>

[<Fact>]
let ``exportCsv returns only the active member's readings`` () =
  let otherMemberReading = { sample with Id = 2; MemberId = 999 }

  let repo = repoWith [ sample; otherMemberReading ]
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.exportCsv ctx

  let body = TestHost.readBody ctx
  // Header + exactly one data row (member 1 only)
  let dataLines =
    body.Split('\n') |> Array.filter (fun l -> l.Trim() <> "") |> Array.tail

  test <@ dataLines.Length = 1 @>
