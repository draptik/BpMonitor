module BpMonitor.Import.Tests.MarkdownImportTests

open System
open BpMonitor.Core
open BpMonitor.Import.MarkdownImport
open Swensen.Unquote
open Xunit

[<Fact>]
let ``parse reading line - no comment`` () =
  let date = DateOnly(2024, 10, 15)
  let line = "- 11.00: 131/80 80"
  let result = parseLine date line

  let expected =
    Some
      { Systolic = 131
        Diastolic = 80
        HeartRate = 80
        Timestamp = DateTimeOffset(2024, 10, 15, 11, 0, 0, TimeSpan.Zero)
        Comments = None }

  test <@ result = expected @>

[<Fact>]
let ``parse reading line - with comment`` () =
  let date = DateOnly(2024, 10, 19)
  let line = "- 19:15: 123/89 98 evening"
  let result = parseLine date line

  let expected =
    Some
      { Systolic = 123
        Diastolic = 89
        HeartRate = 98
        Timestamp = DateTimeOffset(2024, 10, 19, 19, 15, 0, TimeSpan.Zero)
        Comments = Some "evening" }

  test <@ result = expected @>

[<Theory>]
[<InlineData("2024-10-15")>]
[<InlineData("2024-10-18 day comment")>]
[<InlineData("# Some stuff to ignore")>]
[<InlineData("<!-- Some more stuff to ignore -->")>]
[<InlineData("")>]
let ``parse non-reading lines returns None`` (line: string) =
  let date = DateOnly(2024, 10, 15)
  let result = parseLine date line
  test <@ result = None @>

[<Fact>]
let ``parse markdown - multiple dates and readings`` () =
  let markdown =
    """2024-10-15
- 11.00: 131/80 80
- 12.00: 125/76 75
2024-10-16
- 09.30: 118/74 70"""

  let result = parseMarkdown markdown

  let expected =
    [ { Systolic = 131
        Diastolic = 80
        HeartRate = 80
        Timestamp = DateTimeOffset(2024, 10, 15, 11, 0, 0, TimeSpan.Zero)
        Comments = None }
      { Systolic = 125
        Diastolic = 76
        HeartRate = 75
        Timestamp = DateTimeOffset(2024, 10, 15, 12, 0, 0, TimeSpan.Zero)
        Comments = None }
      { Systolic = 118
        Diastolic = 74
        HeartRate = 70
        Timestamp = DateTimeOffset(2024, 10, 16, 9, 30, 0, TimeSpan.Zero)
        Comments = None } ]

  test <@ result = expected @>

[<Fact>]
let ``parse markdown - full sample from issue`` () =
  let markdown =
    """2024-10-17
- 6:15: 108/72 65
# Some stuff to ignore
2024-10-18 day comment
- 9:45: 123/89 98
<!-- Some more stuff to ignore -->

2024-10-19 day comment2
- 9:45: 123/89 98 morning
- 19:15: 123/89 98 evening"""

  let result = parseMarkdown markdown

  let expected =
    [ { Systolic = 108
        Diastolic = 72
        HeartRate = 65
        Timestamp = DateTimeOffset(2024, 10, 17, 6, 15, 0, TimeSpan.Zero)
        Comments = None }
      { Systolic = 123
        Diastolic = 89
        HeartRate = 98
        Timestamp = DateTimeOffset(2024, 10, 18, 9, 45, 0, TimeSpan.Zero)
        Comments = None }
      { Systolic = 123
        Diastolic = 89
        HeartRate = 98
        Timestamp = DateTimeOffset(2024, 10, 19, 9, 45, 0, TimeSpan.Zero)
        Comments = Some "morning" }
      { Systolic = 123
        Diastolic = 89
        HeartRate = 98
        Timestamp = DateTimeOffset(2024, 10, 19, 19, 15, 0, TimeSpan.Zero)
        Comments = Some "evening" } ]

  test <@ result = expected @>

[<Fact>]
let ``parse markdown - trailing date line without readings is ignored`` () =
  let markdown =
    """2024-10-15
- 11.00: 131/80 80
2024-10-16"""

  let result = parseMarkdown markdown

  let expected =
    [ { Systolic = 131
        Diastolic = 80
        HeartRate = 80
        Timestamp = DateTimeOffset(2024, 10, 15, 11, 0, 0, TimeSpan.Zero)
        Comments = None } ]

  test <@ result = expected @>

[<Fact>]
let ``parse markdown - reading without preceding date is ignored`` () =
  let markdown =
    """- 11.00: 131/80 80
2024-10-15
- 12.00: 125/76 75"""

  let result = parseMarkdown markdown

  let expected =
    [ { Systolic = 125
        Diastolic = 76
        HeartRate = 75
        Timestamp = DateTimeOffset(2024, 10, 15, 12, 0, 0, TimeSpan.Zero)
        Comments = None } ]

  test <@ result = expected @>

[<Fact>]
let ``parse reading line - multi-word comment`` () =
  let date = DateOnly(2024, 10, 15)
  let line = "- 9:45: 123/89 98 after morning coffee"
  let result = parseLine date line

  let expected =
    Some
      { Systolic = 123
        Diastolic = 89
        HeartRate = 98
        Timestamp = DateTimeOffset(2024, 10, 15, 9, 45, 0, TimeSpan.Zero)
        Comments = Some "after morning coffee" }

  test <@ result = expected @>

[<Fact>]
let ``parse reading line - single-digit hour with dot separator`` () =
  let date = DateOnly(2024, 10, 17)
  let line = "- 6.15: 108/72 65"
  let result = parseLine date line

  let expected =
    Some
      { Systolic = 108
        Diastolic = 72
        HeartRate = 65
        Timestamp = DateTimeOffset(2024, 10, 17, 6, 15, 0, TimeSpan.Zero)
        Comments = None }

  test <@ result = expected @>

[<Fact>]
let ``parse markdown - empty input returns empty list`` () =
  let result = parseMarkdown ""
  test <@ result = [] @>

[<Fact>]
let ``parse markdown - only ignored lines returns empty list`` () =
  let markdown =
    """# heading
<!-- comment -->

# another heading"""

  let result = parseMarkdown markdown
  test <@ result = [] @>
