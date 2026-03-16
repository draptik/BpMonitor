# BpMonitor — Claude Project Instructions

## Project Overview

A personal blood pressure monitoring app. See `docs/vision.md` for full product vision and `docs/architecture.md` for technical decisions.

## Personas

**At the start of every session, read this file unconditionally:**

- `docs/personas/tdd-sideshow-bob.md` — TDD rules apply to all code changes

All persona rules are **always in effect** — read the relevant persona file before any related action and follow every rule in it without exception. Ignorance of a rule is not an excuse.

| Persona | File | Model |
| --- | --- | --- |
| Product Visionary (Lisa) | `docs/personas/product-visionary-lisa.md` | Haiku 4.5 |
| Architect (Professor Frink) | `docs/personas/architect-frink.md` | Opus 4.6 |
| Senior Developer / TDD (Sideshow Bob) | `docs/personas/tdd-sideshow-bob.md` | Sonnet 4.6 |

To switch: *"Switch to the Architect persona"* or reference the file directly.

When switching to a persona, write the character name to `~/.claude/current-persona` so the status bar reflects it. Example:

```bash
echo "Professor Frink" > ~/.claude/current-persona
```

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
├── BpMonitor.Tui         # Terminal.Gui v2 app (data entry + navigation)
└── BpMonitor.Reports     # Plotly.NET chart generation → HTML output
docs/                     # Product vision, architecture, personas
```

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

## Docs

- `docs/vision.md` — product vision and requirements
- `docs/architecture.md` — tech stack and architectural decisions
- `docs/personas/` — persona definitions (Lisa, Frink, Sideshow Bob)
- `.claude/skills/` — skill definitions (git-workflow, review-tests, setup-tooling, model-advisor, status-bar)
