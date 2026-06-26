namespace BpMonitor.Core

type BloodPressureReadingUnvalidated =
  { Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option }

type BloodPressureReading =
  { Id: int
    MemberId: int
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option
    CreatedAt: System.DateTimeOffset
    ModifiedAt: System.DateTimeOffset }

type ValidationError =
  | SystolicOutOfRange of int
  | DiastolicOutOfRange of int
  | HeartRateOutOfRange of int

type ReadingRanges =
  { SystolicMin: int
    SystolicMax: int
    DiastolicMin: int
    DiastolicMax: int
    HeartRateMin: int
    HeartRateMax: int }

module Timestamp =
  let utc year month day hour minute second =
    System.DateTimeOffset(year, month, day, hour, minute, second, System.TimeSpan.Zero)

  let local year month day hour minute second =
    let offset =
      System.TimeZoneInfo.Local.GetUtcOffset(System.DateTime(year, month, day, hour, minute, second))

    System.DateTimeOffset(year, month, day, hour, minute, second, offset)

module Formats =
  let timestamp = "yyyy-MM-dd HH:mm"

  let formatLocal (ts: System.DateTimeOffset) = ts.ToLocalTime().ToString(timestamp)

module ReadingRanges =
  let defaults =
    { SystolicMin = 1
      SystolicMax = 300
      DiastolicMin = 1
      DiastolicMax = 200
      HeartRateMin = 1
      HeartRateMax = 300 }

module BloodPressureReading =
  open FsToolkit.ErrorHandling

  let private validateField
    (selector: BloodPressureReadingUnvalidated -> int)
    (min: int)
    (max: int)
    (errorCtor: int -> ValidationError)
    (input: BloodPressureReadingUnvalidated)
    : Validation<int, ValidationError> =
    let value = selector input

    if value >= min && value <= max then
      Validation.ok value
    else
      Validation.error (errorCtor value)

  let parse
    (ranges: ReadingRanges)
    (input: BloodPressureReadingUnvalidated)
    : Validation<BloodPressureReading, ValidationError> =
    validation {
      let! sys = validateField _.Systolic ranges.SystolicMin ranges.SystolicMax SystolicOutOfRange input
      and! dia = validateField _.Diastolic ranges.DiastolicMin ranges.DiastolicMax DiastolicOutOfRange input
      and! hr = validateField _.HeartRate ranges.HeartRateMin ranges.HeartRateMax HeartRateOutOfRange input

      return
        { Id = 0
          MemberId = 0
          Systolic = sys
          Diastolic = dia
          HeartRate = hr
          Timestamp = input.Timestamp
          Comments = input.Comments
          CreatedAt = System.DateTimeOffset.MinValue
          ModifiedAt = System.DateTimeOffset.MinValue }
    }
