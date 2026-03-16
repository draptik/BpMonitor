# BpMonitor.Pwa

Bolero (F# on Blazor/WASM) PWA for blood pressure entry. Hosted on ASP.NET Core, deployed to GitHub Pages.

## .NET 8 vs .NET 10 compatibility

Bolero 0.24.x does not yet support .NET 10. Two issues must be worked around until a `net10.0`-targeted Bolero release is available.

### 1. Client must target `net8.0`

Bolero 0.24.x ships only `net6.0` and `net8.0` lib targets. When the client project targets `net10.0`, NuGet silently falls back to the `net8.0` assemblies. Those assemblies use WASM JS interop marshalling conventions (e.g. `getI32`) that are no longer present in the .NET 10 WASM runtime. The app builds without warnings but crashes at runtime in the browser with:

```text
TypeError: Cannot read properties of undefined (reading 'getI32')
```

**Fix:** `BpMonitor.Pwa.Client.fsproj` targets `net8.0`. The server project can remain on `net10.0`.

### 2. Server must opt in to serving Blazor scripts as static web assets

.NET 10 changed how the Blazor bootstrap script (`blazor.webassembly.js`) is served. Without explicit opt-in, the server serves an older or mismatched version of the script, which causes the WASM module loader (`dotnet.js`) to fail fetching dynamically imported modules, producing a console error like:

```text
MONO_WASM: Failed to load config file undefined
TypeError: Failed to fetch dynamically imported module: http://localhost:5010/0
```

**Fix:** Add `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` to `BpMonitor.Pwa.Server.fsproj`. This is documented in the [ASP.NET Core 10.0 release notes](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0#blazor-script-as-static-web-asset).

### References

- [fsbolero/Bolero#375 — Upgrade to dotnet9/10?](https://github.com/fsbolero/Bolero/issues/375)
- [fsbolero/Bolero#376 — Upgrading from .NET 9 to .NET 10 fails](https://github.com/fsbolero/Bolero/issues/376)
