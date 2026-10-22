module HealthProbeTests

open System.Net
open System.Net.Http
open System.Threading.Tasks
open Xunit
open Swensen.Unquote
open BpMonitor.Web

type private StubHandler(respond: HttpRequestMessage -> HttpResponseMessage) =
  inherit HttpMessageHandler()
  override _.SendAsync(request, _cancellationToken) = Task.FromResult(respond request)

let private clientReturning (status: HttpStatusCode) =
  new HttpClient(new StubHandler(fun _ -> new HttpResponseMessage(status)))

[<Fact>]
let ``probe exits 0 when health answers 200`` () =
  use client = clientReturning HttpStatusCode.OK
  test <@ HealthProbe.run client "http://127.0.0.1:5000/health" = 0 @>

[<Fact>]
let ``probe exits 1 when health answers 503`` () =
  use client = clientReturning HttpStatusCode.ServiceUnavailable
  test <@ HealthProbe.run client "http://127.0.0.1:5000/health" = 1 @>

[<Fact>]
let ``probe exits 1 when the server is unreachable`` () =
  use client =
    new HttpClient(new StubHandler(fun _ -> raise (HttpRequestException "Connection refused")))

  test <@ HealthProbe.run client "http://127.0.0.1:5000/health" = 1 @>
