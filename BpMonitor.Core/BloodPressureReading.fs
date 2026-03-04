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
        reading.Systolic > 0 && reading.Systolic <= 300
        && reading.Diastolic > 0 && reading.Diastolic <= 200
        && reading.HeartRate > 0 && reading.HeartRate <= 300
