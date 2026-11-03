module ProjectHygieneTests

open System.IO
open System.Xml.Linq
open Swensen.Unquote
open Xunit

let private codeRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

// Relative to the project, so a checkout under e.g. ~/bin/ isn't mistaken for build output.
let private isBuildOutput (projectDir: string) (path: string) =
  let segments =
    Path.GetRelativePath(projectDir, path).Split(Path.DirectorySeparatorChar)

  segments |> Array.exists (fun s -> s = "bin" || s = "obj")

// MSBuild accepts `\` on every OS; Path.Combine on Linux keeps it literal.
let private resolveInclude (projectDir: string) (compileInclude: string) =
  Path.GetFullPath(Path.Combine(projectDir, compileInclude.Replace('\\', Path.DirectorySeparatorChar)))

let private compiledFiles (fsproj: string) =
  let dir = Path.GetDirectoryName(fsproj)

  XDocument.Load(fsproj).Descendants(XName.Get "Compile")
  |> Seq.choose (fun e -> e.Attribute(XName.Get "Include") |> Option.ofObj)
  |> Seq.map (fun a -> resolveInclude dir a.Value)
  |> Set.ofSeq

[<Fact>]
let ``a checkout under a directory named bin is not build output`` () =
  let projectDir = Path.Combine(Path.GetTempPath(), "bin", "repo", "Proj")
  test <@ not (isBuildOutput projectDir (Path.Combine(projectDir, "Foo.fs"))) @>

[<Fact>]
let ``files under the project's own obj directory are build output`` () =
  let projectDir = Path.Combine(Path.GetTempPath(), "repo", "Proj")
  test <@ isBuildOutput projectDir (Path.Combine(projectDir, "obj", "Debug", "AssemblyInfo.fs")) @>

[<Fact>]
let ``a backslash-separated Compile Include resolves to the file on disk`` () =
  let projectDir = Path.Combine(Path.GetTempPath(), "repo", "Proj")
  test <@ resolveInclude projectDir "Views\\Foo.fs" = Path.Combine(projectDir, "Views", "Foo.fs") @>

// An .fs file missing from its .fsproj is never compiled, so its tests silently never run.
[<Fact>]
let ``every F# source file is compiled by its project`` () =
  let orphans =
    [
      for fsproj in Directory.GetFiles(codeRoot, "*.fsproj", SearchOption.AllDirectories) do
        let projectDir = Path.GetDirectoryName(fsproj)
        let compiled = compiledFiles fsproj

        for fs in Directory.GetFiles(projectDir, "*.fs", SearchOption.AllDirectories) do
          if not (isBuildOutput projectDir fs) && not (compiled.Contains fs) then
            Path.GetRelativePath(codeRoot, fs)
    ]

  test <@ orphans = [] @>
