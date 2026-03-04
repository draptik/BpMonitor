namespace BpMonitor.Core

type BloodPressureReading = {
    Id: int
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option
}

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
    let isValid (ranges: ReadingRanges) (reading: BloodPressureReading) =
        reading.Systolic >= ranges.SystolicMin && reading.Systolic <= ranges.SystolicMax
        && reading.Diastolic >= ranges.DiastolicMin && reading.Diastolic <= ranges.DiastolicMax
        && reading.HeartRate >= ranges.HeartRateMin && reading.HeartRate <= ranges.HeartRateMax
