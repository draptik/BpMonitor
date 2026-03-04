module BloodPressureReadingTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core

let private timeProvider = FakeTimeProvider(DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero))

let private validReading = {
    Id = 0
    Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = timeProvider.GetUtcNow()
    Comments = None
}

let private ranges = ReadingRanges.defaults

[<Fact>]
let ``IsValid returns true when reading has valid values`` () =
    test <@ BloodPressureReading.isValid ranges validReading @>

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(301)>]
let ``IsValid returns false when systolic is out of range`` (invalidSystolic: int) =
    test <@ not (BloodPressureReading.isValid ranges { validReading with Systolic = invalidSystolic }) @>

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(201)>]
let ``IsValid returns false when diastolic is out of range`` (invalidDiastolic: int) =
    test <@ not (BloodPressureReading.isValid ranges { validReading with Diastolic = invalidDiastolic }) @>

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(301)>]
let ``IsValid returns false when heart rate is out of range`` (invalidHeartRate: int) =
    test <@ not (BloodPressureReading.isValid ranges { validReading with HeartRate = invalidHeartRate }) @>
