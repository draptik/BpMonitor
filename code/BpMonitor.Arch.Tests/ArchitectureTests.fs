module ArchitectureTests

open System.Reflection
open ArchUnitNET.Loader
open ArchUnitNET.Fluent
open ArchUnitNET.xUnit
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Import.MarkdownImport
open Xunit

let private chartsAssembly = Assembly.Load("BpMonitor.Charts")
let private exportAssembly = Assembly.Load("BpMonitor.Export")
let private webAssembly = Assembly.Load("BpMonitor.Web")

let private architecture =
  ArchLoader()
    .LoadAssemblies(
      typeof<BloodPressureReading>.Assembly,
      typeof<EfReadingRepository>.Assembly,
      typeof<ImportSummary>.Assembly,
      chartsAssembly,
      exportAssembly,
      webAssembly
    )
    .Build()

// When running under MTP code coverage, a StaticManagedTrackerTemplate type gets
// injected into every instrumented assembly. ArchUnitNET sees the same tracker type
// across multiple assemblies and incorrectly reports cross-assembly dependencies.
// Filter out Microsoft.CodeCoverage types to prevent these false positives.
let private appTypes (assembly: System.Reflection.Assembly) =
  ArchRuleDefinition
    .Types()
    .That()
    .ResideInAssembly(assembly)
    .And()
    .DoNotResideInNamespaceMatching("Microsoft\\.CodeCoverage.*")

let private coreTypes = appTypes typeof<BloodPressureReading>.Assembly
let private dataTypes = appTypes typeof<EfReadingRepository>.Assembly
let private importTypes = appTypes typeof<ImportSummary>.Assembly
let private chartsTypes = appTypes chartsAssembly
let private exportTypes = appTypes exportAssembly
let private webTypes = appTypes webAssembly

[<Fact>]
let ``Core should not depend on Data`` () =
  let rule = coreTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Import should not depend on Data`` () =
  let rule = importTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Import should not depend on Charts`` () =
  let rule = importTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Import should not depend on Export`` () =
  let rule = importTypes.Should().NotDependOnAny(exportTypes)
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
let ``Export should not depend on Import`` () =
  let rule = exportTypes.Should().NotDependOnAny(importTypes)
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
let ``Import should not depend on Web`` () =
  let rule = importTypes.Should().NotDependOnAny(webTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Web`` () =
  let rule = exportTypes.Should().NotDependOnAny(webTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Web should not depend on Import`` () =
  let rule = webTypes.Should().NotDependOnAny(importTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Charts`` () =
  let rule = coreTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Import`` () =
  let rule = coreTypes.Should().NotDependOnAny(importTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Export`` () =
  let rule = coreTypes.Should().NotDependOnAny(exportTypes)
  ArchRuleAssert.CheckRule(architecture, rule)
