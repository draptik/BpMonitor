namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// The `/settings` page: composes the goal-range fragment (`MemberViews.goalRangeSection`)
/// and the medications CRUD section (`MedicationViews.medicationsSection`) under one shell.
module SettingsViews =
  let settings
    (s: LocalizedStrings)
    (memberName: string)
    (isAdmin: bool)
    (language: Language)
    (goalErrors: string list)
    (goalInput: Binding.GoalRangeFormModel)
    (medications: Medication list)
    (medicationErrors: string list)
    : XmlNode =
    ViewLayout.layout
      s
      Routes.settings
      memberName
      isAdmin
      s.Shell.NavSettings
      (Elem.h1 [] [ Text.raw s.Shell.NavSettings ]
       :: MemberViews.languageSection s language
       @ MemberViews.goalRangeSection s goalErrors goalInput
       @ MedicationViews.medicationsSection s medications medicationErrors)
