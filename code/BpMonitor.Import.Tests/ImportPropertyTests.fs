module BpMonitor.Import.Tests.ImportPropertyTests

open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Import
open BpMonitor.Import.Tests.Generators

let private emptyRepo () =
  InMemoryReadingRepository(Some []) :> IReadingRepository

let private withLines (readings: BloodPressureReadingUnvalidated list) =
  readings |> List.mapi (fun i r -> (i + 1, "", r))

[<Property>]
let ``markdown import accounts for every input row`` () =
  Prop.forAll (Arb.fromGen mixedUnvalidatedListGen) (fun readings ->
    let summary =
      MarkdownImport.import (emptyRepo ()) ReadingRanges.defaults (withLines readings)

    summary.Added + summary.Updated + summary.Failed.Length = readings.Length)

[<Property>]
let ``markdown re-import of valid distinct readings updates all and adds none`` () =
  Prop.forAll (Arb.fromGen distinctValidUnvalidatedGen) (fun readings ->
    let repo = emptyRepo ()
    let rows = withLines readings
    MarkdownImport.import repo ReadingRanges.defaults rows |> ignore
    let second = MarkdownImport.import repo ReadingRanges.defaults rows

    second.Added = 0 && second.Updated = readings.Length && second.Failed.IsEmpty)

[<Property>]
let ``json import accounts for every input reading`` () =
  Prop.forAll (Arb.fromGen (Gen.listOf readingGen)) (fun readings ->
    let summary = JsonImport.import (emptyRepo ()) readings
    summary.Added + summary.Updated = readings.Length)

[<Property>]
let ``json re-import of distinct readings updates all and adds none`` () =
  Prop.forAll (Arb.fromGen distinctValidReadingsGen) (fun readings ->
    let repo = emptyRepo ()
    JsonImport.import repo readings |> ignore
    let second = JsonImport.import repo readings

    second.Added = 0 && second.Updated = readings.Length)
