module BpMonitor.Pwa.Client.Reading

open System

type Reading =
  { Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: DateTimeOffset
    Comment: string option }
