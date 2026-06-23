namespace BpMonitor.Core

type GoalRange =
  { SystolicMin: int
    SystolicMax: int
    DiastolicMin: int
    DiastolicMax: int }

type GoalRangeError =
  | SystolicRangeInvalid
  | DiastolicRangeInvalid

/// Where a reading falls relative to the goal range, for the Fig. 5-style
/// (Wegier et al. 2021) value-strip color-coding: out-of-range readings are
/// highlighted, in-range readings are left neutral.
type RangePosition =
  | Below
  | InRange
  | Above

module GoalRange =
  let defaults =
    { SystolicMin = 90
      SystolicMax = 140
      DiastolicMin = 60
      DiastolicMax = 90 }

  let create (sysMin: int) (sysMax: int) (diaMin: int) (diaMax: int) : Result<GoalRange, GoalRangeError> =
    if sysMin >= sysMax then
      Error SystolicRangeInvalid
    elif diaMin >= diaMax then
      Error DiastolicRangeInvalid
    else
      Ok
        { SystolicMin = sysMin
          SystolicMax = sysMax
          DiastolicMin = diaMin
          DiastolicMax = diaMax }

  let classifySystolic (goal: GoalRange) (value: int) : RangePosition =
    if value > goal.SystolicMax then Above
    elif value < goal.SystolicMin then Below
    else InRange

  let classifyDiastolic (goal: GoalRange) (value: int) : RangePosition =
    if value > goal.DiastolicMax then Above
    elif value < goal.DiastolicMin then Below
    else InRange
