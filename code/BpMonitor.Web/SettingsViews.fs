namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// The `/settings` page: composes the goal-range fragment (`MemberViews.goalRangeSection`)
/// and the medications CRUD section (`MedicationViews.medicationsSection`) under one shell.
module SettingsViews =
  let settings
    (s: Strings)
    (memberName: string)
    (isAdmin: bool)
    (language: Language)
    (goalErrors: string list)
    (sysMin: string)
    (sysMax: string)
    (diaMin: string)
    (diaMax: string)
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
       @ MemberViews.goalRangeSection s goalErrors sysMin sysMax diaMin diaMax
       @ MedicationViews.medicationsSection s medications medicationErrors)
