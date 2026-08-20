namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// The `/settings` page: composes the goal-range fragment (`MemberViews.goalRangeSection`)
/// and the medications CRUD section (`MedicationViews.medicationsSection`) under one shell.
module SettingsViews =
  let settings
    (memberName: string)
    (isAdmin: bool)
    (goalErrors: string list)
    (sysMin: string)
    (sysMax: string)
    (diaMin: string)
    (diaMax: string)
    (medications: Medication list)
    (medicationErrors: string list)
    : XmlNode =
    ViewLayout.layout
      Routes.settings
      memberName
      isAdmin
      "Settings"
      (Elem.h1 [] [ Text.raw "Settings" ]
       :: MemberViews.goalRangeSection goalErrors sysMin sysMax diaMin diaMax
       @ MedicationViews.medicationsSection medications medicationErrors)
