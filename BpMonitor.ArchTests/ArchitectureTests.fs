module ArchitectureTests

open ArchUnitNET.Loader
open ArchUnitNET.Fluent
open ArchUnitNET.xUnit
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Tui
open Xunit

let private architecture =
    ArchLoader()
        .LoadAssemblies(
            typeof<BloodPressureReading>.Assembly,
            typeof<EfReadingRepository>.Assembly,
            typeof<DataEntryWindow>.Assembly
        )
        .Build()

let private coreTypes = ArchRuleDefinition.Types().That().ResideInAssembly(typeof<BloodPressureReading>.Assembly)
let private dataTypes = ArchRuleDefinition.Types().That().ResideInAssembly(typeof<EfReadingRepository>.Assembly)
let private tuiTypes  = ArchRuleDefinition.Types().That().ResideInAssembly(typeof<DataEntryWindow>.Assembly)

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
