namespace BpMonitor.Core

type IMedicationRepository =
  abstract GetAll: memberId: int -> Medication list
  abstract Add: memberId: int -> Medication -> unit
  abstract Update: Medication -> unit
  abstract Delete: memberId: int -> id: int -> unit
