namespace BpMonitor.Core

type IMedicationRepository =
  abstract GetAll: memberId: int -> Medication list
  /// The member's medication with that Id; None for another member's or an unknown one.
  abstract TryGet: memberId: int -> id: int -> Medication option
  abstract Add: memberId: int -> Medication -> unit
  abstract Update: Medication -> unit
  abstract Delete: memberId: int -> id: int -> unit
