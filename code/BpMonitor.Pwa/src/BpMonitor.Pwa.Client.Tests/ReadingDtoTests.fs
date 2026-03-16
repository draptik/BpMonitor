module ReadingDtoTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Pwa.Client.Reading
open BpMonitor.Pwa.Client.ReadingDto

let private reading: Reading =
  { Systolic = 120
    Diastolic = 80
    HeartRate = 65
    Timestamp = DateTimeOffset(2026, 3, 16, 9, 0, 0, TimeSpan.Zero)
    Comment = None }

[<Fact>]
let ``toDto serialises timestamp as ISO 8601`` () =
  let dto = toDto reading
  test <@ dto.Timestamp = "2026-03-16T09:00:00+00:00" @>

[<Fact>]
let ``toDto maps None comment to null`` () =
  let dto = toDto reading
  test <@ isNull dto.Comment @>

[<Fact>]
let ``toDto maps Some comment to string`` () =
  let dto =
    toDto
      { reading with
          Comment = Some "after exercise" }

  test <@ dto.Comment = "after exercise" @>

[<Fact>]
let ``fromDto parses ISO 8601 timestamp`` () =
  let dto = toDto reading
  let r = fromDto dto
  test <@ r.Timestamp = reading.Timestamp @>

[<Fact>]
let ``fromDto maps null comment to None`` () =
  let dto = toDto reading
  let r = fromDto dto
  test <@ r.Comment = None @>

[<Fact>]
let ``fromDto maps non-null comment to Some`` () =
  let dto =
    toDto
      { reading with
          Comment = Some "after exercise" }

  let r = fromDto dto
  test <@ r.Comment = Some "after exercise" @>

[<Fact>]
let ``round-trip preserves all fields`` () =
  let withComment = { reading with Comment = Some "test" }
  test <@ fromDto (toDto withComment) = withComment @>
