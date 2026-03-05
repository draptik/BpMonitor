namespace BpMonitor.Core

type BloodPressureReadingUnvalidated = {
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option
}

type BloodPressureReading = {
    Id: int
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option
}

type ValidationError =
    | SystolicOutOfRange of int
    | DiastolicOutOfRange of int
    | HeartRateOutOfRange of int

type ReadingRanges = {
    SystolicMin: int
    SystolicMax: int
    DiastolicMin: int
    DiastolicMax: int
    HeartRateMin: int
    HeartRateMax: int
}

module ReadingRanges =
    let defaults = {
        SystolicMin = 1
        SystolicMax = 300
        DiastolicMin = 1
        DiastolicMax = 200
        HeartRateMin = 1
        HeartRateMax = 300
    }

module BloodPressureReading =
    let parse (ranges: ReadingRanges) (input: BloodPressureReadingUnvalidated) : Result<BloodPressureReading, ValidationError list> =
        let errors = [
            if input.Systolic  < ranges.SystolicMin  || input.Systolic  > ranges.SystolicMax  then yield SystolicOutOfRange  input.Systolic
            if input.Diastolic < ranges.DiastolicMin || input.Diastolic > ranges.DiastolicMax then yield DiastolicOutOfRange input.Diastolic
            if input.HeartRate < ranges.HeartRateMin || input.HeartRate > ranges.HeartRateMax then yield HeartRateOutOfRange  input.HeartRate
        ]
        match errors with
        | [] ->
            Ok {
                Id = 0
                Systolic  = input.Systolic
                Diastolic = input.Diastolic
                HeartRate = input.HeartRate
                Timestamp = input.Timestamp
                Comments  = input.Comments
            }
        | _  -> Error errors
