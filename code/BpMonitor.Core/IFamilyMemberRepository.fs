namespace BpMonitor.Core

type IFamilyMemberRepository =
  abstract GetAll: unit -> FamilyMember list
  abstract GetById: int -> FamilyMember option
  abstract Add: FamilyMember -> FamilyMember
  abstract Update: FamilyMember -> unit
