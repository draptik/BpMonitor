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

## Model Selection

Default to **Sonnet 4.6** (`/model claude-sonnet-4-6`) for everyday work — code, TDD, refactoring. Escalate to **Opus 4.8** (`/model claude-opus-4-8`) only when a task genuinely needs deep reasoning (architecture, tricky tradeoffs, high-stakes judgment), then drop back. See the `model-advisor` skill for the full cheatsheet.

## Skills

The following are available as slash commands (defined in `.claude/skills/`):

| Skill | Command | Description |
| --- | --- | --- |
| Git Workflow | auto-loaded | Branching, commits, PR conventions |
| Ship | `/ship` | Commit, push, open PR, wait for CI, squash-merge, and clean up in one go |
| Review Tests | `/review-tests [file]` | Identify test gaps after implementation |
| Setup Tooling | `/setup-tooling` | Audit and configure code quality infrastructure |
| Model Advisor | auto-loaded | Model selection reference |
| Status Bar | auto-loaded | Status bar configuration reference |
| Verify Frontend | `/verify-frontend` | Verify a frontend change via a throwaway E2E test against a real browser |

## Project Structure

```text
code/
├── BpMonitor.slnx
├── BpMonitor.Core              # Domain models (BloodPressureReading + FamilyMember), interfaces, PasswordHashing, business logic
├── BpMonitor.Core.Tests
├── BpMonitor.Data              # EF Core + SQLite, member-scoped repository implementations
├── BpMonitor.Data.Tests
├── BpMonitor.Export            # JSON and CSV serialisation and file write (wired into Web for /export and /export.csv endpoints)
├── BpMonitor.Export.Tests
├── BpMonitor.Charts            # Plotly.NET chart generation → HTML output
├── BpMonitor.Charts.Tests
├── BpMonitor.Web               # Falco web app (login + per-member auth, landing, add, history, recent, trends, settings, members pages); Serilog structured stdout logging
├── BpMonitor.Web.Tests
├── BpMonitor.Web.E2E.Tests     # Playwright .NET browser smoke tests against a real out-of-process BpMonitor.Web instance
├── BpMonitor.Arch.Tests        # ArchUnit Clean Architecture rules
└── BpMonitor.TestSupport       # Shared test infrastructure (Verify snapshot settings) for *.Tests projects
docs/                           # Product vision, architecture, ADRs
scripts/                        # Dev tooling scripts (e.g. extract-plotly-js.fsx)
```

## Documentation

Keep `docs/architecture.md` and `AGENTS.md` in sync with every structural change. When a PR adds a project, library, tool, or architectural decision, update both files in the same branch before merging.

## Git Workflow

Follow the `git-workflow` skill (`.claude/skills/git-workflow/SKILL.md`). Key rules:
- Gitmoji commit messages
- Feature branches only, never commit to `main`
- Squash merge via PR
- Keep PRs small and focused
- **NEVER add `Co-Authored-By:` trailers to commits** — no Claude attribution, no exceptions

**Before making any code changes**, always create a feature branch first:

```bash
git checkout -b feat/<short-description>
```

Never start work on `main`. Creating the branch is the first step, not an afterthought.

## F# Style Conventions

- Use shorthand lambda syntax where the argument is only used for a single member access chain: `_.Property` instead of `fun x -> x.Property`
- Do not use `.[n]` indexer syntax outside of Unquote quotation expressions (`<@ ... @>`); use `[n]` instead
- Inside Unquote quotation expressions, `[n]` only works on simple local variables — use `.[n]` when indexing the result of a method call (e.g. `repo.GetAll().[0]`)
- `new` is required for any type implementing `IDisposable` — the compiler enforces this via FS0760

## Docs

- `docs/vision.md` — product vision and requirements
- `docs/architecture.md` — tech stack, architectural decisions, and dev tooling (mise, Biome)
- `docs/install.md` — end-user install guide (Web app)
- `CONTRIBUTING.md` — developer setup, linting, testing, changelog convention, release process
- `CHANGELOG.md` — version history in Keep a Changelog format; add `[Unreleased]` bullets in each PR for user-facing changes
- `.claude/skills/` — skill definitions (git-workflow, review-tests, setup-tooling, model-advisor, status-bar)

## Testing

Tests run on **Microsoft.Testing.Platform (MTP)** — all 8 test projects execute in parallel (default: CPU count concurrent modules):

```bash
# Run all tests in parallel (local dev)
dotnet test --configuration Release

# Run with coverage (matches CI)
dotnet test --configuration Release --results-directory ./TestResults -- --coverage --coverage-output-format cobertura
# → produces one GUID-named *.cobertura.xml per project under TestResults/

# Limit parallelism (e.g. on a 2-core machine)
dotnet test --configuration Release --max-parallel-test-modules 2
```

`BpMonitor.Arch.Tests` uses `DoNotResideInNamespaceMatching("Microsoft\\.CodeCoverage.*")` in its type filters to exclude coverage-injected tracker types from ArchUnitNET dependency checks.

`BpMonitor.Web.E2E.Tests` boots a real `BpMonitor.Web` instance out-of-process (`dotnet run`, fresh temp SQLite file, demo seeding off) on a free port, then drives it with a real Playwright Chromium browser. First run needs Chromium installed locally — run `mise run test:e2e-setup` (builds the test project, then installs Chromium via `playwright.ps1` if `pwsh` is available, otherwise via `dotnet fsi` calling `Microsoft.Playwright.Program.Main([| "install"; "chromium" |])`).

## Dev Tooling

All non-dotnet linter versions are pinned in `mise.toml` (repo root); it also hosts one-time dev setup tasks like the Playwright Chromium install. Run `mise install` once after cloning.

- `biome.json` — Biome JS linter config (scoped to the hand-written `wwwroot/` files: `theme.js`, `theme-label.js`, `plot-ready.js`, `chart-hover.js`, `recent-scrubber.js`, `trends-scroll.js`, `recent-zoom.js`)
- `tsconfig.json` + `typings/globals.d.ts` — zero-build TypeScript checking of the same `wwwroot/` JS via JSDoc (`tsc --checkJs`); the JS ships as-is, no bundler
- `.markdownlint-cli2.yaml` — markdownlint config
- Run `mise run lint` to run all non-dotnet linters; `mise run lint:js` / `lint:ts` / `lint:md` / `lint:shell` individually
- Run `mise run test:e2e-setup` once locally before the first `BpMonitor.Web.E2E.Tests` run (installs Playwright's Chromium)
- Run `mise exec -- biome check --write` to auto-fix JS issues
