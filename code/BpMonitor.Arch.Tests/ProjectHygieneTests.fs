module ProjectHygieneTests

open System.IO
open System.Xml.Linq
open Swensen.Unquote
open Xunit

let private codeRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

let private isBuildOutput (path: string) =
  let sep = Path.DirectorySeparatorChar
  path.Contains($"{sep}bin{sep}") || path.Contains($"{sep}obj{sep}")

let private compiledFiles (fsproj: string) =
  let dir = Path.GetDirectoryName(fsproj)

  XDocument.Load(fsproj).Descendants(XName.Get "Compile")
  |> Seq.choose (fun e -> e.Attribute(XName.Get "Include") |> Option.ofObj)
  |> Seq.map (fun a -> Path.GetFullPath(Path.Combine(dir, a.Value)))
  |> Set.ofSeq

// An .fs file missing from its .fsproj is never compiled, so its tests silently never run.
[<Fact>]
let ``every F# source file is compiled by its project`` () =
  let orphans =
    [
      for fsproj in Directory.GetFiles(codeRoot, "*.fsproj", SearchOption.AllDirectories) do
        let compiled = compiledFiles fsproj

        for fs in Directory.GetFiles(Path.GetDirectoryName(fsproj), "*.fs", SearchOption.AllDirectories) do
          if not (isBuildOutput fs) && not (compiled.Contains fs) then
            Path.GetRelativePath(codeRoot, fs)
    ]

  test <@ orphans = [] @>
