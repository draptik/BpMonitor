module ArchitectureTests

open System.Reflection
open ArchUnitNET.Loader
open ArchUnitNET.Fluent
open ArchUnitNET.xUnit
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Import.MarkdownImport
open BpMonitor.Tui
open Xunit

let private chartsAssembly = Assembly.Load("BpMonitor.Charts")
let private exportAssembly = Assembly.Load("BpMonitor.Export")

let private architecture =
  ArchLoader()
    .LoadAssemblies(
      typeof<BloodPressureReading>.Assembly,
      typeof<EfReadingRepository>.Assembly,
      typeof<ImportSummary>.Assembly,
      typeof<ReadingsWindow>.Assembly,
      chartsAssembly,
      exportAssembly
    )
    .Build()

let private coreTypes =
  ArchRuleDefinition.Types().That().ResideInAssembly(typeof<BloodPressureReading>.Assembly)

let private dataTypes =
  ArchRuleDefinition.Types().That().ResideInAssembly(typeof<EfReadingRepository>.Assembly)

let private importTypes =
  ArchRuleDefinition.Types().That().ResideInAssembly(typeof<ImportSummary>.Assembly)

let private tuiTypes =
  ArchRuleDefinition.Types().That().ResideInAssembly(typeof<ReadingsWindow>.Assembly)

let private chartsTypes =
  ArchRuleDefinition.Types().That().ResideInAssembly(chartsAssembly)

let private exportTypes =
  ArchRuleDefinition.Types().That().ResideInAssembly(exportAssembly)

[<Fact>]
let ``Core should not depend on Data`` () =
  let rule = coreTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Core should not depend on Tui`` () =
  let rule = coreTypes.Should().NotDependOnAny(tuiTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Data should not depend on Tui`` () =
  let rule = dataTypes.Should().NotDependOnAny(tuiTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Import should not depend on Data`` () =
  let rule = importTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Import should not depend on Tui`` () =
  let rule = importTypes.Should().NotDependOnAny(tuiTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Import should not depend on Charts`` () =
  let rule = importTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Charts should not depend on Data`` () =
  let rule = chartsTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Charts should not depend on Tui`` () =
  let rule = chartsTypes.Should().NotDependOnAny(tuiTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Data`` () =
  let rule = exportTypes.Should().NotDependOnAny(dataTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Tui`` () =
  let rule = exportTypes.Should().NotDependOnAny(tuiTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Charts`` () =
  let rule = exportTypes.Should().NotDependOnAny(chartsTypes)
  ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Export should not depend on Import`` () =
  let rule = exportTypes.Should().NotDependOnAny(importTypes)
  ArchRuleAssert.CheckRule(architecture, rule)
