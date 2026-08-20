namespace BpMonitor.Data

open BpMonitor.Core

type InMemoryMedicationRepository(initialMedications: Medication list option) =
  let medications = ResizeArray<Medication>(defaultArg initialMedications [])

  let mutable nextId =
    let initial = defaultArg initialMedications []

    if initial.IsEmpty then
      1
    else
      (initial |> List.map _.Id |> List.max) + 1

  interface IMedicationRepository with
    member _.GetAll(memberId) =
      medications |> Seq.filter (fun m -> m.MemberId = memberId) |> Seq.toList

    member _.Add memberId medication =
      medications.Add(
        { medication with
            Id = nextId
            MemberId = memberId }
      )

      nextId <- nextId + 1

    member _.Update(medication) =
      let idx =
        medications
        |> Seq.tryFindIndex (fun m -> m.Id = medication.Id && m.MemberId = medication.MemberId)

      match idx with
      | Some i -> medications[i] <- medication
      | None -> ()

    member _.Delete memberId id =
      let idx =
        medications |> Seq.tryFindIndex (fun m -> m.Id = id && m.MemberId = memberId)

      match idx with
      | Some i -> medications.RemoveAt(i)
      | None -> ()
