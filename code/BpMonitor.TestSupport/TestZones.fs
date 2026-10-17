module BpMonitor.TestSupport.TestZones

open System

/// Has DST (last Sunday of March 02:00 → 03:00, last Sunday of October 03:00 → 02:00).
let berlin = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"

/// A half-hour offset with no DST, far from Berlin.
let kolkata = TimeZoneInfo.FindSystemTimeZoneById "Asia/Kolkata"
