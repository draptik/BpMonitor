namespace BpMonitor.Core

type IReadingRepository =
  abstract GetAll: unit -> BloodPressureReading list
  abstract Add: BloodPressureReading -> unit
  abstract Update: BloodPressureReading -> unit
