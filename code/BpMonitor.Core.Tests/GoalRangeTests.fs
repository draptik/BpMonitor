module GoalRangeTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core

[<Fact>]
let ``defaults match the paper's preset goal range`` () =
  let expected: GoalRange =
    { SystolicMin = 90
      SystolicMax = 140
      DiastolicMin = 60
      DiastolicMax = 90 }

  test <@ GoalRange.defaults = expected @>

[<Fact>]
let ``create with valid bounds returns Ok GoalRange`` () =
  let expected: GoalRange =
    { SystolicMin = 100
      SystolicMax = 130
      DiastolicMin = 65
      DiastolicMax = 85 }

  test <@ GoalRange.create 100 130 65 85 = Ok expected @>

[<Fact>]
let ``create rejects systolic min equal to max`` () =
  test <@ GoalRange.create 130 130 65 85 = Error SystolicRangeInvalid @>

[<Fact>]
let ``create rejects systolic min greater than max`` () =
  test <@ GoalRange.create 140 100 65 85 = Error SystolicRangeInvalid @>

[<Fact>]
let ``create rejects diastolic min equal to max`` () =
  test <@ GoalRange.create 100 130 85 85 = Error DiastolicRangeInvalid @>

[<Fact>]
let ``create rejects diastolic min greater than max`` () =
  test <@ GoalRange.create 100 130 90 65 = Error DiastolicRangeInvalid @>
