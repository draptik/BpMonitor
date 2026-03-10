module ArchitectureTests

open System.Reflection
open ArchUnitNET.Loader
open ArchUnitNET.Fluent
open ArchUnitNET.xUnit
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Tui
open Xunit

let private chartsAssembly = Assembly.Load("BpMonitor.Charts")

let private architecture =
    ArchLoader()
        .LoadAssemblies(
            typeof<BloodPressureReading>.Assembly,
            typeof<EfReadingRepository>.Assembly,
            typeof<ReadingsWindow>.Assembly,
            chartsAssembly
        )
        .Build()

let private coreTypes   = ArchRuleDefinition.Types().That().ResideInAssembly(typeof<BloodPressureReading>.Assembly)
let private dataTypes   = ArchRuleDefinition.Types().That().ResideInAssembly(typeof<EfReadingRepository>.Assembly)
let private tuiTypes    = ArchRuleDefinition.Types().That().ResideInAssembly(typeof<ReadingsWindow>.Assembly)
let private chartsTypes = ArchRuleDefinition.Types().That().ResideInAssembly(chartsAssembly)

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
let ``Charts should not depend on Data`` () =
    let rule = chartsTypes.Should().NotDependOnAny(dataTypes)
    ArchRuleAssert.CheckRule(architecture, rule)

[<Fact>]
let ``Charts should not depend on Tui`` () =
    let rule = chartsTypes.Should().NotDependOnAny(tuiTypes)
    ArchRuleAssert.CheckRule(architecture, rule)
