namespace BpMonitor.Core

type BloodPressureReading = {
    Id: int
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option
}

module BloodPressureReading =
    let isValid (reading: BloodPressureReading) =
        reading.Systolic > 0
