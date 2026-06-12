module JsonRoundTripPropertyTests

open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Export
open BpMonitor.Import
open Generators

[<Property>]
let ``serialize then parse round-trips a reading list`` () =
  Prop.forAll (Arb.fromGen (Gen.listOf readingGen)) (fun readings ->
    JsonImport.parse (JsonExport.serialize readings) = Ok readings)
