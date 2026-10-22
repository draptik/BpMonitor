namespace BpMonitor.Web

open System.Net.Http

/// Container health check client: chiseled images ship no curl or shell to poll /health with.
module HealthProbe =
  let run (client: HttpClient) (url: string) : int =
    try
      use response = client.GetAsync(url).GetAwaiter().GetResult()
      if response.IsSuccessStatusCode then 0 else 1
    with _ ->
      1
