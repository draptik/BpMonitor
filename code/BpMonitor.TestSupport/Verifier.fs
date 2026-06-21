module BpMonitor.TestSupport.Verifier

open System.Text.RegularExpressions
open System.Threading.Tasks
open Argon
open VerifyTests

/// Verify resolves the snapshot directory from the caller's source file.
/// Since these helpers live outside the calling test project, callers must
/// pass their own file path explicitly (e.g. via __SOURCE_DIRECTORY__ /
/// __SOURCE_FILE__) rather than relying on CallerFilePathAttribute, which
/// would anchor to this module instead of the calling test file.

let private customizedVerifySettings () =
  let settings = VerifySettings()
  settings.UseDirectory "VerifiedSnapshots"
  settings.AddExtraSettings(fun s -> s.NullValueHandling <- NullValueHandling.Include)

  settings.ScrubInlineGuids()

  settings.AddScrubber(fun sb ->
    let scrubbed =
      Regex.Replace(string sb, @"renderPlotly_[0-9a-f]{32}", "renderPlotly_GUID")

    sb.Clear().Append(scrubbed) |> ignore)

  settings

let verify (sourceFilePath: string) (value: 't :> obj) : Task =
  VerifyXunit.Verifier.Verify(value :> obj, customizedVerifySettings (), sourceFilePath).ToTask() :> Task

let verifyHtml (sourceFilePath: string) (value: string) : Task =
  let target = Target(extension = "html", data = value)

  VerifyXunit.Verifier.Verify(target, customizedVerifySettings (), sourceFilePath).ToTask() :> Task

let verifyXml (sourceFilePath: string) (value: string) : Task =
  VerifyXunit.Verifier.VerifyXml(value, customizedVerifySettings (), sourceFilePath).ToTask() :> Task

let verifyJson (sourceFilePath: string) (value: string) : Task =
  let settings = customizedVerifySettings ()
  // VerifyJson defaults to a quote-stripped, .verified.txt snapshot; strict
  // mode keeps it valid JSON and gives it a .verified.json extension instead.
  settings.UseStrictJson()
  VerifyXunit.Verifier.VerifyJson(value, settings, sourceFilePath).ToTask() :> Task
