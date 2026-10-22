namespace BpMonitor.Core

open System
open System.Globalization

type BloodPressureReadingUnvalidated =
  {
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: DateTimeOffset
    Comments: string option
  }

type BloodPressureReading =
  {
    Id: int
    MemberId: int
    Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: DateTimeOffset
    Comments: string option
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset
  }

type ValidationError =
  | SystolicOutOfRange of int
  | DiastolicOutOfRange of int
  | HeartRateOutOfRange of int

type ReadingRanges =
  {
    SystolicMin: int
    SystolicMax: int
    DiastolicMin: int
    DiastolicMax: int
    HeartRateMin: int
    HeartRateMax: int
  }

module Timestamp =
  let utc year month day hour minute second =
    DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)

  let local year month day hour minute second =
    let offset =
      TimeZoneInfo.Local.GetUtcOffset(DateTime(year, month, day, hour, minute, second))

    DateTimeOffset(year, month, day, hour, minute, second, offset)

module Formats =
  let timestamp = "yyyy-MM-dd HH:mm"

  let formatLocal (ts: DateTimeOffset) =
    ts.ToLocalTime().ToString(timestamp, CultureInfo.InvariantCulture)

  let date = "yyyy-MM-dd"

  let formatDate (d: DateOnly) =
    d.ToString(date, CultureInfo.InvariantCulture)

  let dateEuropean = "dd.MM.yyyy"

  let formatDateEuropean (d: DateOnly) =
    d.ToString(dateEuropean, CultureInfo.InvariantCulture)

module ReadingRanges =
  let defaults =
    {
      SystolicMin = 1
      SystolicMax = 300
      DiastolicMin = 1
      DiastolicMax = 200
      HeartRateMin = 1
      HeartRateMax = 300
    }

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
      let! sys =
        validateField _.Systolic ranges.SystolicMin ranges.SystolicMax SystolicOutOfRange input

      and! dia =
        validateField _.Diastolic ranges.DiastolicMin ranges.DiastolicMax DiastolicOutOfRange input

      and! hr =
        validateField _.HeartRate ranges.HeartRateMin ranges.HeartRateMax HeartRateOutOfRange input

      return
        {
          Id = 0
          MemberId = 0
          Systolic = sys
          Diastolic = dia
          HeartRate = hr
          Timestamp = input.Timestamp
          Comments = input.Comments
          CreatedAt = DateTimeOffset.MinValue
          ModifiedAt = DateTimeOffset.MinValue
        }
    }
