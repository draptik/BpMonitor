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

[<Fact>]
let ``classifySystolic reports Above when the value exceeds the goal max`` () =
  test <@ GoalRange.classifySystolic GoalRange.defaults 141 = Above @>

[<Fact>]
let ``classifySystolic reports Below when the value is under the goal min`` () =
  test <@ GoalRange.classifySystolic GoalRange.defaults 89 = Below @>

[<Fact>]
let ``classifySystolic reports InRange for a value strictly inside the goal range`` () =
  test <@ GoalRange.classifySystolic GoalRange.defaults 120 = InRange @>

[<Fact>]
let ``classifySystolic treats the goal min and max boundaries as InRange`` () =
  test
    <@
      GoalRange.classifySystolic GoalRange.defaults GoalRange.defaults.SystolicMin = InRange
      && GoalRange.classifySystolic GoalRange.defaults GoalRange.defaults.SystolicMax = InRange
    @>

[<Fact>]
let ``classifyDiastolic reports Above when the value exceeds the goal max`` () =
  test <@ GoalRange.classifyDiastolic GoalRange.defaults 91 = Above @>

[<Fact>]
let ``classifyDiastolic reports Below when the value is under the goal min`` () =
  test <@ GoalRange.classifyDiastolic GoalRange.defaults 59 = Below @>

[<Fact>]
let ``classifyDiastolic treats the goal min and max boundaries as InRange`` () =
  test
    <@
      GoalRange.classifyDiastolic GoalRange.defaults GoalRange.defaults.DiastolicMin = InRange
      && GoalRange.classifyDiastolic GoalRange.defaults GoalRange.defaults.DiastolicMax = InRange
    @>
