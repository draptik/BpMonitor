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
| Review Tests | `/review-tests [file]` | Identify test gaps after implementation |
| Setup Tooling | `/setup-tooling` | Audit and configure code quality infrastructure |
| Model Advisor | auto-loaded | Model selection reference |
| Status Bar | auto-loaded | Status bar configuration reference |

## Project Structure

```text
code/
├── BpMonitor.slnx
├── BpMonitor.Core        # Domain models, interfaces, business logic
├── BpMonitor.Data        # EF Core + SQLite, repository implementations
├── BpMonitor.Import      # Markdown and JSON importers
├── BpMonitor.Export      # JSON serialisation and file write
├── BpMonitor.Charts      # Plotly.NET chart generation → HTML output
├── BpMonitor.Tui         # Terminal.Gui v2 app (data entry + navigation)
├── BpMonitor.Web         # Falco web app (landing, add, history pages)
└── BpMonitor.Arch.Tests  # ArchUnit Clean Architecture rules
docs/                     # Product vision, architecture, ADRs
```

## Documentation

Keep `docs/architecture.md` and `AGENTS.md` in sync with every structural change. When a PR adds a project, library, tool, or architectural decision, update both files in the same branch before merging.

## Git Workflow

Follow the `git-workflow` skill (`.claude/skills/git-workflow/SKILL.md`). Key rules:
- Gitmoji commit messages
- Feature branches only, never commit to `main`
- Squash merge via PR
- Keep PRs small and focused

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
- `.claude/skills/` — skill definitions (git-workflow, review-tests, setup-tooling, model-advisor, status-bar)

## Dev Tooling

Tool versions are pinned in `mise.toml` (repo root). Run `mise install` once after cloning.

- `biome.json` — Biome JS linter config (scoped to `wwwroot/theme.js` and `wwwroot/theme-label.js`)
- Run `mise exec -- biome check` to lint; `mise exec -- biome check --write` to auto-fix
