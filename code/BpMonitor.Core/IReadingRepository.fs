namespace BpMonitor.Core

type IReadingRepository =
  abstract GetAll: memberId: int -> BloodPressureReading list
  /// The member's reading with that Id; None for another member's or an unknown one.
  abstract TryGet: memberId: int -> id: int -> BloodPressureReading option
  abstract Add: memberId: int -> BloodPressureReading -> unit
  abstract AddMany: memberId: int -> BloodPressureReading list -> unit
  abstract Update: BloodPressureReading -> unit
