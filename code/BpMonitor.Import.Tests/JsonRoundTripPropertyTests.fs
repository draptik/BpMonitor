module BpMonitor.Import.Tests.JsonRoundTripPropertyTests

open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Export
open BpMonitor.Import
open BpMonitor.Import.Tests.Generators

[<Property>]
let ``serialize then parse round-trips a reading list`` () =
  Prop.forAll (Arb.fromGen (Gen.listOf readingGen)) (fun readings ->
    JsonImport.parse (JsonExport.serialize readings) = Ok readings)
