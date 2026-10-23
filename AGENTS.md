# BpMonitor — Claude Project Instructions

## Project Overview

A personal blood pressure monitoring app. See `docs/vision.md` for full product vision and `docs/architecture.md` for technical decisions.

## Working Rules

These rules are always in effect — follow them for any related action.

### Test-Driven Development

All code changes follow strict TDD (Red → Green → Refactor):

1. **Red:** Write the smallest failing test that describes the desired behavior
2. **Green:** Write the minimum code to make the test pass — no more
3. **Refactor:** Clean up without changing behavior; tests still pass

- NEVER write implementation code before a failing test exists
- Tests drive design — if something is hard to test, the design is wrong
- Each test must have a single, clear reason to fail
- After writing a test, **pause and ask whether it captures the right behavior** before implementing
- Keep the loop tight: one test → one implementation step at a time
- Do not gold-plate: implement only what the test requires
- Edge cases emerge as the suite grows — don't enumerate them all upfront

### Right altitude for the task

- **Product/vision work:** stay at the level of problems, users, and outcomes — no implementation talk
- **Architecture work:** discuss tradeoffs honestly, flag risks and unknowns, ask clarifying questions before recommending
- **Implementation work:** follow the TDD cycle above

### Spelling

Use American spelling everywhere — identifiers, comments, test names, docs, ADRs, CHANGELOG
(e.g. neighbor, color, behavior, serialization, centered). Leave vendored files untouched.

## Model Selection

Default to **Sonnet** (`/model sonnet`) for everyday work — code, TDD, refactoring. Escalate to **Opus** (`/model opus`) only when a task genuinely needs deep reasoning (architecture, tricky tradeoffs, high-stakes judgment), then drop back. See the `model-advisor` skill for the full cheatsheet.

## Skills

The following are available as slash commands (defined in `.claude/skills/`):

| Skill | Command | Description |
| --- | --- | --- |
| Git Workflow | auto-loaded | Branching, commits, PR conventions |
| Ship | `/ship` | Push already-committed work, open PR, wait for CI, squash-merge, and clean up in one go (Haiku subagent running `scripts/ship-pr.sh --yes`; refuses uncommitted changes) |
| Review Tests | `/review-tests [file]` | Identify test gaps after implementation (forked subagent; returns a prioritized list) |
| Setup Tooling | `/setup-tooling` | Audit and configure code quality infrastructure |
| Model Advisor | auto-loaded | Model selection reference |
| Status Bar | auto-loaded | Status bar configuration reference |
| Verify Frontend | `/verify-frontend` | Verify a frontend change via a throwaway E2E test against a real browser |

## Project Structure

```text
code/
├── BpMonitor.slnx
├── BpMonitor.Core              # Domain models (BloodPressureReading + FamilyMember), interfaces, PasswordHashing, SetupCode, business logic; owns the LocalizedStrings/Language string table (see ADR 0008)
├── BpMonitor.Core.Tests
├── BpMonitor.Data              # EF Core + SQLite, member-scoped repository implementations
├── BpMonitor.Data.Tests
├── BpMonitor.Export            # JSON and CSV serialization and file write (wired into Web for /export and /export.csv endpoints)
├── BpMonitor.Export.Tests
├── BpMonitor.Charts            # Plotly.NET chart generation → HTML output
├── BpMonitor.Charts.Tests
├── BpMonitor.Web               # Falco web app (login + per-member auth, landing, add, history, recent, trends, settings, members, health pages); Serilog structured stdout logging
├── BpMonitor.Web.Tests
├── BpMonitor.Web.E2E.Tests     # Playwright .NET browser smoke tests against a real out-of-process BpMonitor.Web instance
├── BpMonitor.Arch.Tests        # ArchUnit Clean Architecture rules
├── BpMonitor.Benchmarks        # BenchmarkDotNet suite (LOWESS reference vs. sliding window); run with `mise run bench`, not part of `dotnet test`
└── BpMonitor.TestSupport       # Shared test infrastructure (Verify snapshot settings, domain record builders, E2E process-readiness polling) for *.Tests projects
docs/                           # Product vision, architecture, ADRs
scripts/                        # Dev tooling scripts (e.g. extract-plotly-js.fsx, release-notes.sh)
```

## Documentation

Keep `docs/architecture.md` and `AGENTS.md` in sync with every structural change. When a PR adds a project, library, tool, or architectural decision, update both files in the same branch before merging.

## Git Workflow

Follow the `git-workflow` skill (`.claude/skills/git-workflow/SKILL.md`). Key rules:
- Gitmoji commit messages
- Feature branches only, never commit to `main` *(enforced by hook)*
- Squash merge via PR
- Keep PRs small and focused
- **NEVER add `Co-Authored-By:` trailers to commits** — no Claude attribution, no exceptions *(enforced by hook)*
- PR bodies are a single `## Summary` section — no other headings, no "Generated with Claude Code" footer *(enforced by hook)*

**Before making any code changes**, always create a feature branch first:

```bash
git checkout -b feat/<short-description>
```

Never start work on `main`. Creating the branch is the first step, not an afterthought.

`.claude/hooks/` enforces branch-first, no-attribution, PR preflight (CHANGELOG/docs sync
reminders) and `/verify-frontend`. `.husky/` runs formatting, typos, Vale and zizmor on staged
files, blocks commits on `main` or without a `feat/`/`fix/`/`chore/` branch prefix, and rejects
a gitmoji that doesn't match its conventional-commit type.

## F# Style Conventions

- Use shorthand lambda syntax where the argument is only used for a single member access chain: `_.Property` instead of `fun x -> x.Property`
  - Covers property/method/indexer chains rooted at the parameter, e.g. `_.Timestamp.ToLocalTime().Date`, `_.Contains("x")`, `_.Groups[1].Value`
  - Does not apply if the parameter is used more than once (e.g. `fun r -> r.Id = r.OtherId`) or if the chain result is piped into another function (e.g. `fun a -> a.GetValue() |> Option.ofObj` — `_.GetValue() |> Option.ofObj` type-checks against the wrong thing, since `|>` binds to the whole lambda, not its result)
- Do not use `.[n]` indexer syntax outside of Unquote quotation expressions (`<@ ... @>`); use `[n]` instead
- Inside Unquote quotation expressions, `[n]` only works on simple local variables — use `.[n]` when indexing the result of a method call (e.g. `repo.GetAll().[0]`)
- `new` is required for any type implementing `IDisposable` — the compiler enforces this via FS0760

## Docs

- `docs/vision.md` — product vision and requirements
- `docs/architecture.md` — tech stack, architectural decisions, and dev tooling (mise, Biome)
- `docs/install.md` — end-user install guide (Web app)
- `CONTRIBUTING.md` — developer setup, linting, testing, changelog convention, release process
- `CHANGELOG.md` — version history in Keep a Changelog format; add `[Unreleased]` bullets in each PR for user-facing changes
- `.claude/skills/` — skill definitions (see Skills above)

## Testing

Tests run on **Microsoft.Testing.Platform (MTP)** — all 7 test projects execute in parallel (default: CPU count concurrent modules):

```bash
# Run all tests in parallel (local dev)
dotnet test --configuration Release

# Run with coverage (CI runs the same, but as two --filter-namespace-split
# steps with --ignore-exit-code 8, so E2E isn't fighting the rest for CPU)
dotnet test --configuration Release --results-directory ./TestResults -- --coverage --coverage-output-format cobertura
# → produces one GUID-named *.cobertura.xml per project under TestResults/

# Limit parallelism (e.g. on a 2-core machine)
dotnet test --configuration Release --max-parallel-test-modules 2

# Run a subset of tests: MTP, not VSTest — there is no top-level --filter.
# Target one project directly and pass the filter after `--`:
dotnet test BpMonitor.Web.Tests -- --filter-method "*SomeTestName*"
```

`BpMonitor.Arch.Tests` uses `DoNotResideInNamespaceMatching("Microsoft\\.CodeCoverage.*")` in its type filters to exclude coverage-injected tracker types from ArchUnitNET dependency checks.

`BpMonitor.Web.E2E.Tests` boots one out-of-process `BpMonitor.Web` (fresh temp SQLite, no demo data) shared by the whole assembly; each test class gets its own family member, not its own app — one app per class was the CI flakiness source (`AppFixture`, `ChromiumFixture`/`FirefoxFixture` in `WebAppFixture.fs`). Firefox catches spec violations Chromium tolerates (e.g. non-finite `MouseEventInit` fields). Run `mise run test:e2e-setup` once to install both browsers.

## Dev Tooling

All non-dotnet tools are pinned in `mise.toml` (run `mise install` once after cloning); see
`docs/architecture.md` (Development Tooling) for what each one checks and where its config lives.

- `mise run lint` runs all non-dotnet linters; `CONTRIBUTING.md` lists the individual `lint:*` tasks. `lint:links` needs the network and isn't part of `lint`
- `mise exec -- biome check --write` auto-fixes JS; `cd code && dotnet fantomas .` reformats F#
- Hand-written `wwwroot/*.js` is linted by Biome and type-checked via JSDoc (`tsc --checkJs`); it ships as-is, no bundler
- Real words that typos flags go in `_typos.toml` `[default.extend-words]`; new house-style terms get a Vale rule in `.vale/styles/BpMonitor/`
- `mise run test:scripts` tests `scripts/` (e.g. `check-pr-body.sh`, shared by the attribution hook and `ship-pr.sh`)
- `mise run bench` runs BenchmarkDotNet (a few minutes; `-- --job short` for a quick check)
- `mise run screenshots` regenerates `docs/screenshots/*.png` from a self-seeded demo instance — part of `cut-release`, otherwise only after a UI change worth showing in the README
