module BloodPressureReadingTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core

let private timeProvider = FakeTimeProvider(DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero))

[<Fact>]
let ``IsValid returns true when reading has valid values`` () =
    let reading = {
        Id = 0
        Systolic = 120
        Diastolic = 80
        HeartRate = 70
        Timestamp = timeProvider.GetUtcNow()
        Comments = None
    }
    test <@ BloodPressureReading.isValid reading @>
