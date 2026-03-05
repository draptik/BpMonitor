namespace BpMonitor.Data

open System

[<CLIMutable>]
type ReadingRecord = {
    Id        : int
    Systolic  : int
    Diastolic : int
    HeartRate : int
    Timestamp : DateTimeOffset
    Comments  : string          // null represents absent comment
}
