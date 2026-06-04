namespace BpMonitor.Core

type IReadingRepository =
  abstract GetAll: memberId: int -> BloodPressureReading list
  abstract Add: memberId: int -> BloodPressureReading -> unit
  abstract AddMany: memberId: int -> BloodPressureReading list -> unit
  abstract Update: BloodPressureReading -> unit
