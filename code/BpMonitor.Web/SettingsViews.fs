namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// The `/settings` page: composes the goal-range fragment (`MemberViews.goalRangeSection`)
/// and the medications CRUD section (`MedicationViews.medicationsSection`) under one shell.
module SettingsViews =
  /// What the settings page shows besides the member's own fields.
  type SettingsModel =
    {
      GoalInput: Binding.GoalRangeFormModel
      GoalErrors: string list
      Medications: Medication list
      MedicationErrors: string list
    }

  /// The page as stored: the member's goal and medications, no errors.
  let model (goal: GoalRange) (medications: Medication list) : SettingsModel =
    {
      GoalInput = Binding.ofGoal goal
      GoalErrors = []
      Medications = medications
      MedicationErrors = []
    }

  let settings (s: LocalizedStrings) (m: FamilyMember) (model: SettingsModel) : XmlNode =
    ViewLayout.layout
      s
      Routes.settings
      m.Name
      m.IsAdmin
      s.Shell.NavSettings
      [
        Elem.div
          [ Attr.class' "dense-page" ]
          (Elem.h1 [] [ Text.raw s.Shell.NavSettings ]
           :: MemberViews.languageSection s m.Language
           @ MemberViews.goalRangeSection s model.GoalErrors model.GoalInput
           @ MedicationViews.medicationsSection s model.Medications model.MedicationErrors)
      ]
