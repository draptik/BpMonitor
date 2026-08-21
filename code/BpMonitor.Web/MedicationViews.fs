namespace BpMonitor.Web

open Falco.Markup
open BpMonitor.Core

/// Server-rendered HTML views for medications: the `/settings` CRUD section and the
/// collapsible Medications Timeline panel embedded below the BP chart on /recent and
/// /history (Wegier et al. 2021 Fig. 5).
module MedicationViews =
  /// Required/optional badge shown next to a field's label.
  let private requirementBadge (required: bool) : XmlNode =
    if required then
      Elem.span [ Attr.class' "field-badge field-required" ] [ Text.raw "Required" ]
    else
      Elem.span [ Attr.class' "field-badge field-optional" ] [ Text.raw "Optional" ]

  let private field
    (labelText: string)
    (hint: string option)
    (required: bool)
    (name: string)
    (value: string)
    (inputType: string)
    =
    Elem.div
      [ Attr.class' "field" ]
      [ Elem.label [ Attr.for' name ] [ Text.raw labelText; requirementBadge required ]
        match hint with
        | Some h -> Elem.small [ Attr.class' "field-hint" ] [ Text.raw h ]
        | None -> Text.raw ""
        Elem.input [ Attr.type' inputType; Attr.id name; Attr.name name; Attr.value value ] ]

  let private fieldWithHint
    (labelText: string)
    (hint: string)
    (required: bool)
    (name: string)
    (value: string)
    (inputType: string)
    =
    field labelText (Some hint) required name value inputType

  let private medicationRow (m: Medication) : XmlNode =
    Elem.tr
      []
      [ Elem.td [] [ Text.enc m.Name ]
        Elem.td [] [ Text.enc (m.FullName |> Option.defaultValue "") ]
        Elem.td [ Attr.class' "text-center" ] [ Text.enc (Formats.formatDateEuropean m.StartDate) ]
        Elem.td
          [ Attr.class' "text-center" ]
          [ Text.enc (m.EndDate |> Option.map Formats.formatDateEuropean |> Option.defaultValue "") ]
        Elem.td [] [ Text.enc (m.Comment |> Option.defaultValue "") ]
        Elem.td
          [ Attr.class' "member-actions" ]
          [ Elem.a
              [ Attr.href (Routes.medicationEdit m.Id)
                Attr.role "button"
                Attr.class' "outline secondary" ]
              [ Text.raw "Edit" ]
            ViewLayout.inlineDangerPostButton
              (Routes.medicationDelete m.Id)
              "Delete"
              $"Delete {m.Name}? This cannot be undone." ] ]

  /// The `/settings` Medications section: a collapsible table plus inline add form.
  let medicationsSection (medications: Medication list) (errors: string list) : XmlNode list =
    [ Elem.details
        [ Attr.class' "settings-section"
          Attr.create "open" ""
          Attr.create "data-persist-key" "settings-medications" ]
        [ Elem.summary [] [ Elem.h2 [] [ Text.raw "Medications" ] ]
          ViewLayout.errorBox errors
          Elem.table
            []
            [ Elem.thead
                []
                [ Elem.tr
                    []
                    [ Elem.th [] [ Text.raw "Name" ]
                      Elem.th [] [ Text.raw "Full name" ]
                      Elem.th [ Attr.class' "text-center" ] [ Text.raw "Start" ]
                      Elem.th [ Attr.class' "text-center" ] [ Text.raw "End" ]
                      Elem.th [] [ Text.raw "Comment" ]
                      Elem.th [] [ Text.raw "" ] ] ]
              Elem.tbody [] (medications |> List.sortBy _.StartDate |> List.map medicationRow) ]
          Elem.h2 [] [ Text.raw "Add medication" ]
          Elem.form
            [ Attr.method "post"; Attr.action Routes.medications; Attr.class' "stacked" ]
            [ fieldWithHint
                "Name"
                "Short label shown on the timeline, e.g. HCTZ"
                true
                FormFields.medicationName
                ""
                "text"
              fieldWithHint
                "Full name"
                "Long form, shown in the timeline's hover tooltip"
                false
                FormFields.medicationFullName
                ""
                "text"
              field "Comment" None false FormFields.medicationComment "" "text"
              fieldWithHint "Start date" "dd.mm.yyyy" true FormFields.medicationStartDate "" "text"
              fieldWithHint "End date" "dd.mm.yyyy" false FormFields.medicationEndDate "" "text"
              Elem.button [ Attr.type' "submit" ] [ Text.raw "Add medication" ] ] ] ]

  /// Shared add/edit form for a single medication. `action` is the POST target;
  /// `errors` are rendered above the fields when re-displaying after a failed submit.
  let medicationForm
    (memberName: string)
    (isAdmin: bool)
    (title: string)
    (action: string)
    (errors: string list)
    (name: string)
    (fullName: string)
    (comment: string)
    (startDate: string)
    (endDate: string)
    : XmlNode =
    ViewLayout.layout
      Routes.settings
      memberName
      isAdmin
      title
      [ Elem.h1 [] [ Text.raw title ]
        ViewLayout.errorBox errors
        Elem.form
          [ Attr.method "post"; Attr.action action ]
          [ fieldWithHint
              "Name"
              "Short label shown on the timeline, e.g. HCTZ"
              true
              FormFields.medicationName
              name
              "text"
            fieldWithHint
              "Full name"
              "Long form, shown in the timeline's hover tooltip"
              false
              FormFields.medicationFullName
              fullName
              "text"
            field "Comment" None false FormFields.medicationComment comment "text"
            fieldWithHint "Start date" "dd.mm.yyyy" true FormFields.medicationStartDate startDate "text"
            fieldWithHint "End date" "dd.mm.yyyy" false FormFields.medicationEndDate endDate "text"
            ViewLayout.formActions Routes.settings ] ]

  /// The collapsible Medications Timeline panel embedded below the BP chart on /recent
  /// and /history. Renders nothing when there's no chart to show (BpChart.toHtmlMedications
  /// returns "" for an empty medication list) — an empty collapsible panel would just be
  /// visual clutter. `data-persist-key` lets wwwroot/details-memory.js remember whether the
  /// member left it open across page loads.
  let timelinePanel (chartHtml: string) : XmlNode =
    if chartHtml = "" then
      Text.raw ""
    else
      Elem.details
        [ Attr.class' "medications-timeline"
          Attr.create "data-persist-key" "medications-timeline" ]
        [ Elem.summary [] [ Text.raw "Medications Timeline" ]
          // Not the plain `.chart` class: that fixes height to `--chart-height` (420px)
          // for the BP chart, which would stretch/clip this chart's own short, explicit
          // height (Charts.fs medicationsLayout — set to fit its row count).
          Elem.div [ Attr.class' "chart medications-chart" ] [ Text.raw chartHtml ] ]
