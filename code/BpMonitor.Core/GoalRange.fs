namespace BpMonitor.Core

type GoalRange =
  { SystolicMin: int
    SystolicMax: int
    DiastolicMin: int
    DiastolicMax: int }

type GoalRangeError =
  | SystolicRangeInvalid
  | DiastolicRangeInvalid

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
