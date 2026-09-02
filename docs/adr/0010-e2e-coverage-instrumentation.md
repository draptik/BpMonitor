# ADR-0010: Keep Code Coverage Instrumentation on the E2E Test Step (Accepted)

## Status

Accepted

## Context

`BpMonitor.Web.E2E.Tests` drives a real, out-of-process `BpMonitor.Web` instance
(`WebAppFixture.fs`/`AppFixture`) launched via `dotnet exec` and killed on teardown. CI's
`Test (E2E)` step originally ran with `--coverage --coverage-output-format cobertura`,
merged into the same coverage report as the unit/integration step.

While fixing the E2E suite's recurring CI-only flakiness (PR #513), `--coverage` was
dropped from the `Test (E2E)` step on the assumption that it contributed nothing: the app
under test runs in a *separate process*, so the reasoning was that `dotnet test`'s
coverage collector — which instruments the test host — could not reach it. Since
`BpMonitor.TestSupport` also carries `[<assembly: ExcludeFromCodeCoverage>]`, the E2E
step's own cobertura output looked safe to drop for a ~14s-per-boot instrumentation
saving on the step most prone to timing out.

That assumption was checked against a *local* reproduction of the CI commands, which
showed zero `BpMonitor.Web` classes in the E2E step's coverage file — on both the old
(13-processes-per-test-class) and new (one shared process) architecture. It looked
conclusive.

## Finding

It was wrong, and the local reproduction is why: this sandbox's local test runs do not
replicate CI's cross-process coverage instrumentation (most likely a native
profiler-injection or sandboxing difference), so a "0% contribution" measured locally is
not evidence of "0% contribution" in CI — only of a gap in the local environment. A
follow-up CI-only experiment (PR #515, no local step) confirmed the opposite: the app
process launched by the E2E fixture inherits the test host's CLR-profiler environment
variables (`CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, etc.), so it genuinely gets
instrumented, `webProcess.Kill(entireProcessTree = true)` notwithstanding — the coverage
collector's output survives a hard kill of the monitored process.

Measured directly in CI, same commit, coverage on vs. off:

| | Line coverage | Branch coverage | `Test (E2E)` step time |
| --- | --- | --- | --- |
| Without `--coverage` | 93.7% (3416/3642) | 78.4% (757/965) | ~47s |
| With `--coverage` | 96.7% (3525/3642) | 80.1% (773/965) | ~61s |

That's 109 covered lines and 16 covered branches — real `BpMonitor.Web` code paths only
exercised via a real browser (full HTTP pipeline, real htmx/session behavior) — for ~14s
of added step time on the now-single shared app process introduced by #513. Before that
fix, the same instrumentation ran once per test class (13 times); the per-boot overhead
was real but so was the boot-storm timeout risk it stacked onto.

## Decision

**Keep `--coverage --coverage-output-format cobertura` on `Test (E2E)`.** The measured
cost (~14s, one shared app process) is small and bounded; the measured benefit (real
production coverage from browser-only paths) is not reproducible any other way in this
suite.

## Consequences

- CI's coverage report and badge include real E2E-only-covered `BpMonitor.Web` lines
  again; don't be surprised if the number moves ~3pp relative to a run that dropped it.
- **Don't trust a local reproduction of CI coverage percentages for this project.**
  Cross-process instrumentation (anything that depends on a child process inheriting the
  test host's CLR-profiler environment) may not behave the same locally as in CI. Verify
  coverage-instrumentation changes with an actual CI run (a throwaway PR, as here — #515),
  not a local `dotnet test -- --coverage` comparison.
- If the E2E step's wall time becomes a problem again, dropping `--coverage` there is a
  real, quantifiable lever (~14s) — but revisit this ADR rather than reasoning about it
  from first principles again, since the "out-of-process ⇒ uninstrumented" intuition that
  motivated #513 was demonstrably false for this codebase's process-launch shape.
