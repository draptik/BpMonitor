module ArchitectureTests

open System
open System.Reflection
open ArchUnitNET.Loader
open ArchUnitNET.Fluent
open ArchUnitNET.xUnit
open BpMonitor.Core
open BpMonitor.Data
open Xunit

let private chartsAssembly = Assembly.Load("BpMonitor.Charts")
let private exportAssembly = Assembly.Load("BpMonitor.Export")
let private webAssembly = Assembly.Load("BpMonitor.Web")

let private architecture =
  ArchLoader()
    .LoadAssemblies(
      typeof<BloodPressureReading>.Assembly,
      typeof<EfReadingRepository>.Assembly,
      chartsAssembly,
      exportAssembly,
      webAssembly
    )
    .Build()

// When running under MTP code coverage, a StaticManagedTrackerTemplate type gets
// injected into every instrumented assembly. ArchUnitNET sees the same tracker type
// across multiple assemblies and incorrectly reports cross-assembly dependencies.
// Filter out Microsoft.CodeCoverage types to prevent these false positives.
let private appTypes (assembly: Assembly) =
  ArchRuleDefinition
    .Types()
    .That()
    .ResideInAssembly(assembly)
    .And()
    .DoNotResideInNamespaceMatching("Microsoft\\.CodeCoverage.*")

let private coreTypes = appTypes typeof<BloodPressureReading>.Assembly
let private dataTypes = appTypes typeof<EfReadingRepository>.Assembly
let private chartsTypes = appTypes chartsAssembly
let private exportTypes = appTypes exportAssembly
let private webTypes = appTypes webAssembly

[<Fact>]
let ``Core should not depend on Data`` () =
  let rule = coreTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Charts should not depend on Data`` () =
  let rule = chartsTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Data`` () =
  let rule = exportTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Charts`` () =
  let rule = exportTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Web`` () =
  let rule = coreTypes.Should().NotDependOnAny(webTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Data should not depend on Web`` () =
  let rule = dataTypes.Should().NotDependOnAny(webTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Charts should not depend on Web`` () =
  let rule = chartsTypes.Should().NotDependOnAny(webTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Web`` () =
  let rule = exportTypes.Should().NotDependOnAny(webTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Charts`` () =
  let rule = coreTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Export`` () =
  let rule = coreTypes.Should().NotDependOnAny(exportTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Data should not depend on Charts`` () =
  let rule = dataTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Data should not depend on Export`` () =
  let rule = dataTypes.Should().NotDependOnAny(exportTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

// Project rules above don't catch Core picking up a framework dependency directly.
[<Fact>]
let ``Core does not reference EF Core, ASP.NET Core, SQLite, or Falco`` () =
  let forbiddenPrefixes =
    [
      "Microsoft.EntityFrameworkCore"
      "Microsoft.AspNetCore"
      "Microsoft.Data.Sqlite"
      "Falco"
    ]

  let violations =
    typeof<BloodPressureReading>.Assembly.GetReferencedAssemblies()
    |> Array.map _.Name
    |> Array.filter (fun name ->
      forbiddenPrefixes
      |> List.exists (fun prefix -> name.StartsWith(prefix, StringComparison.Ordinal)))

  Assert.Empty(violations)

// Handlers use Core's repository interfaces, not the DbContext — except HealthHandlers' /health probe.
[<Fact>]
let ``Handlers should not depend on BpMonitorDbContext directly`` () =
  let handlerTypes =
    ArchRuleDefinition
      .Types()
      .That()
      .ResideInAssembly(webAssembly)
      .And()
      .HaveNameEndingWith("Handlers")
      .And()
      .DoNotHaveName("HealthHandlers")

  let dbContextType =
    ArchRuleDefinition
      .Types()
      .That()
      .ResideInAssembly(typeof<EfReadingRepository>.Assembly)
      .And()
      .HaveName("BpMonitorDbContext")

  let rule = handlerTypes.Should().NotDependOnAny(dbContextType)
  ArchRuleAssert.CheckRule(architecture, rule)

// Naming convention documented in docs/architecture.md (I*Repository -> Ef*/InMemory*Repository).
[<Fact>]
let ``every I*Repository in Core has matching Ef*Repository and InMemory*Repository implementations`` () =
  let repositoryInterfaces =
    typeof<BloodPressureReading>.Assembly.GetTypes()
    |> Array.filter (fun t -> t.IsInterface && t.Name.StartsWith("I") && t.Name.EndsWith("Repository"))

  let dataImplTypes = typeof<EfReadingRepository>.Assembly.GetTypes()

  let violations =
    repositoryInterfaces
    |> Array.collect (fun iface ->
      let baseName = iface.Name.Substring(1, iface.Name.Length - 1 - "Repository".Length)

      [ $"Ef{baseName}Repository"; $"InMemory{baseName}Repository" ]
      |> List.filter (fun expectedName ->
        dataImplTypes
        |> Array.exists (fun t -> t.Name = expectedName && iface.IsAssignableFrom(t))
        |> not)
      |> List.map (fun expectedName -> $"{iface.Name}: no implementation named '{expectedName}'")
      |> Array.ofList)

  Assert.Empty(violations)

// ADR 0013: local time comes from a zone the caller passes in, never from the host.
// ArchUnitNET doesn't load BCL members, so this reads each method's IL for call targets instead.
let private hostZoneLookups =
  set
    [
      "System.TimeZoneInfo.get_Local"
      "System.DateTimeOffset.ToLocalTime"
      "System.TimeProvider.GetLocalNow"
    ]

/// "Type.Method -> Callee" for every direct call in `assembly` to one of `targets`.
let private callsTo (targets: Set<string>) (assembly: Assembly) : string list =
  let flags =
    BindingFlags.Public
    ||| BindingFlags.NonPublic
    ||| BindingFlags.Instance
    ||| BindingFlags.Static
    ||| BindingFlags.DeclaredOnly

  let resolve (m: MethodBase) (token: int) =
    try
      let typeArgs =
        if m.DeclaringType.IsGenericType then
          m.DeclaringType.GetGenericArguments()
        else
          null

      let methodArgs = if m.IsGenericMethod then m.GetGenericArguments() else null
      Option.ofObj (m.Module.ResolveMethod(token, typeArgs, methodArgs))
    with _ ->
      None

  [
    for t in assembly.GetTypes() do
      for m in Seq.append (t.GetMethods flags |> Seq.cast<MethodBase>) (t.GetConstructors flags |> Seq.cast) do
        match m.GetMethodBody() with
        | null -> ()
        | body ->
          let il = body.GetILAsByteArray()

          for i in 0 .. il.Length - 5 do
            // call (0x28) and callvirt (0x6F) take a 4-byte method token.
            if il[i] = 0x28uy || il[i] = 0x6Fuy then
              match resolve m (BitConverter.ToInt32(il, i + 1)) with
              | Some callee when targets.Contains $"{callee.DeclaringType.FullName}.{callee.Name}" ->
                $"{t.FullName}.{m.Name} -> {callee.DeclaringType.Name}.{callee.Name}"
              | _ -> ()
  ]

[<Fact>]
let ``Core never asks the host for its time zone`` () =
  Assert.Empty(callsTo hostZoneLookups typeof<BloodPressureReading>.Assembly)
