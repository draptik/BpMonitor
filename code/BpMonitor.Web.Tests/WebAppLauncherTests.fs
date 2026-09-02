module WebAppLauncherTests

open BpMonitor.Web.E2E
open Xunit

type WebAppLauncherTests() =

  [<Fact>]
  member _.``resolves the Release web app dll from a Release test output directory``() =
    let dll =
      WebAppLauncher.resolveAppDll "/repo/code" "/repo/code/BpMonitor.Web.E2E.Tests/bin/Release/net10.0"

    Assert.Equal("/repo/code/BpMonitor.Web/bin/Release/net10.0/BpMonitor.Web.dll", dll)

  [<Fact>]
  member _.``resolves the Debug web app dll from a Debug test output directory``() =
    let dll =
      WebAppLauncher.resolveAppDll "/repo/code" "/repo/code/BpMonitor.Web.E2E.Tests/bin/Debug/net10.0"

    Assert.Equal("/repo/code/BpMonitor.Web/bin/Debug/net10.0/BpMonitor.Web.dll", dll)

  [<Fact>]
  member _.``tolerates a trailing directory separator``() =
    let dll =
      WebAppLauncher.resolveAppDll "/repo/code" "/repo/code/BpMonitor.Web.E2E.Tests/bin/Release/net10.0/"

    Assert.Equal("/repo/code/BpMonitor.Web/bin/Release/net10.0/BpMonitor.Web.dll", dll)

  [<Fact>]
  member _.``fails clearly when the output directory doesn't look like a bin/config/tfm path``() =
    let ex =
      Assert.Throws<System.Exception>(fun () ->
        WebAppLauncher.resolveAppDll "/repo/code" "/somewhere/unexpected" |> ignore)

    Assert.Contains("Could not resolve", ex.Message)
