namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for medications: the `/settings` CRUD section and the
/// collapsible Medications Timeline panel embedded below the BP chart on /recent and /history.
module MedicationViews =
  /// Required/optional badge shown next to a field's label.
  let private requirementBadge (s: LocalizedStrings) (required: bool) : XmlNode =
    if required then
      Elem.span [ Attr.class' "field-badge field-required" ] [ Text.raw s.Medication.Required ]
    else
      Elem.span [ Attr.class' "field-badge field-optional" ] [ Text.raw s.Medication.Optional ]

  let private field
    (s: LocalizedStrings)
    (labelText: string)
    (hint: string option)
    (required: bool)
    (name: string)
    (value: string)
    (inputType: string)
    =
    Elem.div
      [ Attr.class' "field" ]
      [
        Elem.label [ Attr.for' name ] [ Text.raw labelText; requirementBadge s required ]
        match hint with
        | Some h -> Elem.small [ Attr.class' "field-hint" ] [ Text.raw h ]
        | None -> Text.raw ""
        Elem.input
          [
            Attr.type' inputType
            Attr.id name
            Attr.name name
            ViewLayout.encAttr "value" value
          ]
      ]

  let private fieldWithHint
    (s: LocalizedStrings)
    (labelText: string)
    (hint: string)
    (required: bool)
    (name: string)
    (value: string)
    (inputType: string)
    =
    field s labelText (Some hint) required name value inputType

  let private medicationRow (s: LocalizedStrings) (m: Medication) : XmlNode =
    Elem.tr
      []
      [
        Elem.td [] [ Text.enc m.Name ]
        Elem.td [] [ Text.enc (m.FullName |> Option.defaultValue "") ]
        Elem.td [ Attr.class' "text-center" ] [ Text.enc (Formats.formatDateEuropean m.StartDate) ]
        Elem.td
          [ Attr.class' "text-center" ]
          [
            Text.enc (m.EndDate |> Option.map Formats.formatDateEuropean |> Option.defaultValue "")
          ]
        Elem.td [] [ Text.enc (m.Comment |> Option.defaultValue "") ]
        Elem.td
          [ Attr.class' "member-actions" ]
          [
            ViewLayout.editLink (s.Shell.EditNamed m.Name) (Routes.medicationEdit m.Id)
            ViewLayout.inlineDeleteButton
              (Routes.medicationDelete m.Id)
              (s.Shell.DeleteNamed m.Name)
              (s.Medication.DeleteConfirm m.Name)
          ]
      ]

  /// The `/settings` Medications section: a collapsible table plus inline add form.
  let medicationsSection (s: LocalizedStrings) (medications: Medication list) (errors: string list) : XmlNode list =
    [
      Elem.details
        [
          Attr.class' "collapsible settings-section"
          Attr.create "open" ""
          Attr.create "data-persist-key" "settings-medications"
        ]
        [
          Elem.summary [] [ Elem.h2 [] [ Text.raw s.Medication.MedicationsTitle ] ]
          ViewLayout.errorBox errors
          Elem.table
            []
            [
              Elem.thead
                []
                [
                  Elem.tr
                    []
                    [
                      Elem.th [] [ Text.raw s.Shell.Name ]
                      Elem.th [] [ Text.raw s.Medication.FullNameHeader ]
                      Elem.th [ Attr.class' "text-center" ] [ Text.raw s.Medication.StartHeader ]
                      Elem.th [ Attr.class' "text-center" ] [ Text.raw s.Medication.EndHeader ]
                      Elem.th [] [ Text.raw s.Shell.Comment ]
                      Elem.th [] [ Text.raw "" ]
                    ]
                ]
              Elem.tbody [] (medications |> List.sortBy _.StartDate |> List.map (medicationRow s))
            ]
          Elem.h2 [] [ Text.raw s.Medication.AddMedicationTitle ]
          Elem.form
            [ Attr.method "post"; Attr.action Routes.medications; Attr.class' "stacked" ]
            [
              fieldWithHint s s.Shell.Name s.Medication.NameHint true FormFields.medicationName "" "text"
              fieldWithHint
                s
                s.Medication.FullNameHeader
                s.Medication.FullNameHint
                false
                FormFields.medicationFullName
                ""
                "text"
              field s s.Shell.Comment None false FormFields.medicationComment "" "text"
              fieldWithHint
                s
                s.Medication.StartDateLabel
                s.Medication.StartDateHint
                true
                FormFields.medicationStartDate
                ""
                "text"
              fieldWithHint
                s
                s.Medication.EndDateLabel
                s.Medication.EndDateHint
                false
                FormFields.medicationEndDate
                ""
                "text"
              Elem.button [ Attr.type' "submit" ] [ Text.raw s.Medication.AddMedicationTitle ]
            ]
        ]
    ]

  /// Edit form for medication `id` (adding happens on /settings); `errors` are
  /// rendered above the fields when re-displaying after a failed submit.
  let medicationForm
    (s: LocalizedStrings)
    (activeMember: FamilyMember)
    (id: int)
    (errors: string list)
    (f: Binding.MedicationFormModel)
    : XmlNode =
    let title = s.Medication.EditMedicationTitle

    ViewLayout.layout
      s
      Routes.settings
      activeMember.Name
      activeMember.IsAdmin
      title
      [
        Elem.h1 [] [ Text.raw title ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action (Routes.medicationUpdate id) ]
          [
            fieldWithHint s s.Shell.Name s.Medication.NameHint true FormFields.medicationName f.Name "text"
            fieldWithHint
              s
              s.Medication.FullNameHeader
              s.Medication.FullNameHint
              false
              FormFields.medicationFullName
              f.FullName
              "text"
            field s s.Shell.Comment None false FormFields.medicationComment f.Comment "text"
            fieldWithHint
              s
              s.Medication.StartDateLabel
              s.Medication.StartDateHint
              true
              FormFields.medicationStartDate
              f.StartDate
              "text"
            fieldWithHint
              s
              s.Medication.EndDateLabel
              s.Medication.EndDateHint
              false
              FormFields.medicationEndDate
              f.EndDate
              "text"
            ViewLayout.formActions s Routes.settings
          ]
      ]

  /// The collapsible Medications Timeline panel embedded below the BP chart on /recent
  /// and /history. Renders nothing when there's no chart to show.
  let timelinePanel (s: LocalizedStrings) (chartHtml: string) : XmlNode =
    if chartHtml = "" then
      Text.raw ""
    else
      Elem.details
        [
          Attr.class' "collapsible medications-timeline"
          Attr.create "data-persist-key" "medications-timeline"
        ]
        [
          Elem.summary [] [ Text.raw s.Medication.MedicationsTimelineTitle ]
          // Not the plain `.chart` class: that fixes height to `--chart-height` for the BP chart.
          Elem.div [ Attr.class' "chart medications-chart" ] [ Text.raw chartHtml ]
        ]
