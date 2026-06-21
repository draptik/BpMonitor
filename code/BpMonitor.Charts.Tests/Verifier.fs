module Verifier

open System.Text.RegularExpressions
open System.Threading.Tasks
open Argon
open VerifyTests
open VerifyXunit

let private verify_internal_with_settings (settings: VerifySettings) (value: 't :> obj) =
  Verifier.Verify(value :> obj, settings).ToTask() :> Task

let private verify_html_internal_with_settings (settings: VerifySettings) (value: string) =
  let target = Target(extension = "html", data = value)
  Verifier.Verify(target, settings).ToTask() :> Task

let private verify_xml_internal_with_settings (settings: VerifySettings) (value: string) =
  Verifier.VerifyXml(value, settings).ToTask() :> Task

let private customizedVerifySettings =
  let settings = VerifySettings()
  settings.UseDirectory "VerifiedSnapshots"
  settings.AddExtraSettings(fun s -> s.NullValueHandling <- NullValueHandling.Include)

  settings.ScrubInlineGuids()

  settings.AddScrubber(fun sb ->
    let scrubbed =
      Regex.Replace(string sb, @"renderPlotly_[0-9a-f]{32}", "renderPlotly_GUID")

    sb.Clear().Append(scrubbed) |> ignore)

  settings

// Public API ---------------------------------------------

let verify value =
  verify_internal_with_settings customizedVerifySettings value

let verifyHtml value =
  verify_html_internal_with_settings customizedVerifySettings value

let verifyXml value =
  verify_xml_internal_with_settings customizedVerifySettings value
