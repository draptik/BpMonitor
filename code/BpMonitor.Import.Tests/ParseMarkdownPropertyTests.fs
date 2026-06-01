module BpMonitor.Import.Tests.ParseMarkdownPropertyTests

open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open BpMonitor.Import.MarkdownImport
open BpMonitor.Import.Tests.Generators

let private render (items: DocItem list) =
  items |> List.map renderDocLine |> String.concat "\n"

/// A clean re-implementation of parseMarkdown's date-carry-forward and filtering,
/// used as an oracle. Lines are joined with '\n', so each item's 1-based line
/// number is its index + 1.
let private expected (items: DocItem list) =
  let folder (currentDate, acc) (i, item) =
    match item with
    | DateHeader d -> (Some d, acc)
    | DateHeaderWithText(d, _) -> (Some d, acc)
    | NoiseRow _ -> (currentDate, acc)
    | ReadingRow(h, m, sys, dia, hr, comment) ->
      match currentDate with
      | None -> (currentDate, acc)
      | Some d ->
        let reading: BloodPressureReadingUnvalidated =
          { Systolic = sys
            Diastolic = dia
            HeartRate = hr
            Timestamp = Timestamp.local d.Year d.Month d.Day h m 0
            Comments = comment }

        (currentDate, (i + 1, renderDocLine item, reading) :: acc)

  items |> List.indexed |> List.fold folder (None, []) |> snd |> List.rev

[<Property>]
let ``parseMarkdown matches the date-carry-forward model`` () =
  Prop.forAll (Arb.fromGen docItemsGen) (fun items -> parseMarkdown (render items) = expected items)

[<Property>]
let ``parseMarkdown emits strictly increasing line numbers`` () =
  Prop.forAll (Arb.fromGen docItemsGen) (fun items ->
    let lineNumbers = parseMarkdown (render items) |> List.map (fun (n, _, _) -> n)
    lineNumbers = List.sort lineNumbers && List.distinct lineNumbers = lineNumbers)

[<Property>]
let ``parseMarkdown triples reference their source line`` () =
  Prop.forAll (Arb.fromGen docItemsGen) (fun items ->
    let lines = items |> List.map renderDocLine

    parseMarkdown (String.concat "\n" lines)
    |> List.forall (fun (n, line, _) -> n >= 1 && n <= lines.Length && lines.[n - 1] = line))
