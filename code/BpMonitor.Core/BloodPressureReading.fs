namespace BpMonitor.Core

type BloodPressureReadingUnvalidated =
  { Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: System.DateTimeOffset
    Comments: string option }

type BloodPressureReading =
  { Id: int
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

  let private validateSystolic ranges (input: BloodPressureReadingUnvalidated) : Validation<int, ValidationError> =
    if input.Systolic >= ranges.SystolicMin && input.Systolic <= ranges.SystolicMax then
      Validation.ok input.Systolic
    else
      Validation.error (SystolicOutOfRange input.Systolic)

  let private validateDiastolic ranges (input: BloodPressureReadingUnvalidated) : Validation<int, ValidationError> =
    if input.Diastolic >= ranges.DiastolicMin && input.Diastolic <= ranges.DiastolicMax then
      Validation.ok input.Diastolic
    else
      Validation.error (DiastolicOutOfRange input.Diastolic)

  let private validateHeartRate ranges (input: BloodPressureReadingUnvalidated) : Validation<int, ValidationError> =
    if input.HeartRate >= ranges.HeartRateMin && input.HeartRate <= ranges.HeartRateMax then
      Validation.ok input.HeartRate
    else
      Validation.error (HeartRateOutOfRange input.HeartRate)

  let parse
    (ranges: ReadingRanges)
    (input: BloodPressureReadingUnvalidated)
    : Validation<BloodPressureReading, ValidationError> =
    validation {
      let! sys = validateSystolic ranges input
      and! dia = validateDiastolic ranges input
      and! hr = validateHeartRate ranges input

      return
        { Id = 0
          Systolic = sys
          Diastolic = dia
          HeartRate = hr
          Timestamp = input.Timestamp
          Comments = input.Comments
          CreatedAt = System.DateTimeOffset.MinValue
          ModifiedAt = System.DateTimeOffset.MinValue }
    }
