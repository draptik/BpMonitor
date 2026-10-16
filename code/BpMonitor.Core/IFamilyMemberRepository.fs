namespace BpMonitor.Core

type IFamilyMemberRepository =
  abstract GetAll: unit -> FamilyMember list
  abstract GetById: int -> FamilyMember option
  abstract Add: FamilyMember -> FamilyMember
  abstract Update: FamilyMember -> unit
  /// Changes only the language (and ModifiedAt), so a concurrent admin change survives.
  abstract UpdateLanguage: id: int -> language: Language -> unit
  /// Changes only the goal range (and ModifiedAt), so a concurrent admin change survives.
  abstract UpdateGoal: id: int -> goal: GoalRange -> unit
