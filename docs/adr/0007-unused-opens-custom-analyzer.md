# ADR-0007: Custom FSharp.Analyzers.SDK Analyzer for Unused Opens (Rejected)

## Status

Rejected

## Context

F# has no built-in warning for unused `open` declarations (unlike unused variables, which
`--warnon:1182` already surfaces). Rider's IDE inspection catches them, but nothing in
`dotnet build` or CI does.

## Finding

No existing tool covers this. FSharpLint's 42 rules have no unused-open rule. The
`fsharp-analyzers` CLI's community packs (G-Research, Ionide) don't either — both confirmed by
injecting a deliberately unused `open` and re-running, which produced zero findings from any
of them. `jb inspectcode` can't run F# at all outside Rider (`Can't find plugin
ReSharper.FSharp` — JetBrains' F# support is Rider-exclusive, not shipped for the headless
CLI). Unused-open detection is an FCS API
(`FSharp.Compiler.EditorServices.UnusedOpens.getUnusedOpens`) that only editor integrations
call — nobody has packaged it as a standalone lint rule.

A prototype custom `FSharp.Analyzers.SDK` analyzer calling that API directly was built and
worked: it found the one genuine unused open in the repo and nothing else. But each run costs
~7–9s regardless of scope (one project or the whole solution) — dominated by FCS type-checking
the target project, not by building the analyzer — with no cheap single-file mode. That rules
out a pre-commit hook.

## Decision

**Not pursued.** Every other lint tool here (Fantomas, Biome, markdownlint, shellcheck) runs
both as a pre-commit hook and in CI. A check that can only ever run in CI, never locally,
breaks that symmetry for a rule that guards hygiene, not correctness — not worth it.

## Consequences

- Unused opens continue to be caught only by IDE inspection, with no CI or pre-commit
  enforcement.
- Revisit if FCS's unused-open check ever gets a cheap single-file mode, or if the project
  decides CI-only hygiene checks (without a pre-commit counterpart) are acceptable.
