# ADR-0002: Mutation Testing with Stryker.NET (Rejected)

## Status

Rejected

## Context

We wanted to raise confidence in the test suite beyond line coverage by adding mutation
testing. Mutation testing deliberately introduces small faults ("mutants") into the code and
checks whether the tests catch them, which exposes weak or assertion-free tests that line
coverage alone cannot detect. [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/) is
the de-facto mutation testing tool in the .NET ecosystem, so it was the natural candidate.

## Finding

BpMonitor is written entirely in **F#** (all production and test projects under `code/`).
Stryker.NET's released tooling mutates **C# only** — it parses and rewrites source via the
Roslyn C# compiler. F# support exists upstream solely as incomplete groundwork that the
official documentation describes as work "added to secure F# support in the future"; it is
not wired into the published tool and is not production-ready.

See the official current-state page:
<https://stryker-mutator.io/docs/stryker-net/technical-reference/fsharp/current-state/>

There is also no mature alternative mutation-testing tool for F# (e.g. Fettle is C#-only and
inactive).

## Decision

Mutation testing with Stryker.NET is **not pursued** at this time. No spike was run and no
tooling, configuration, or CI changes were made.

## Consequences

- Test-quality confidence continues to rely on the existing xUnit v3 test suite together
  with coverlet + ReportGenerator line-coverage reporting (the `build-and-test` job in
  `.github/workflows/ci.yml`).
- Revisit this decision if Stryker.NET ships production-ready F# support, or if a mature F#
  mutation-testing tool emerges.
