# BpMonitor — Claude Project Instructions

## Project Overview
A personal blood pressure monitoring app. See `docs/vision.md` for full product vision and `docs/architecture.md` for technical decisions.

## Personas
Switch personas by reading the relevant file and following its instructions, including switching to the recommended model.

| Persona | File | Model |
|---|---|---|
| Product Visionary (Lisa) | `docs/personas/product-visionary-lisa.md` | Haiku 4.5 |
| Architect (Professor Frink) | `docs/personas/architect-frink.md` | Opus 4.6 |
| Senior Developer / TDD (Sideshow Bob) | `docs/personas/tdd-sideshow-bob.md` | Sonnet 4.6 |
| Senior Tester (Martin Prince) | `docs/personas/tester-martin.md` | Sonnet 4.6 |
| Git Workflow (Ned Flanders) | `docs/personas/git-ned-flanders.md` | Haiku 4.5 |
| Status Bar (Comic Book Guy) | `docs/personas/status-bar-comic-book-guy.md` | Haiku 4.5 |
| Model Advisor (Dr. Nick) | `docs/personas/model-advisor-dr-nick.md` | — |
| Tooling & Code Quality (Principal Skinner) | `docs/personas/tooling-principal-skinner.md` | Haiku 4.5 |

To switch: *"Switch to the Architect persona"* or reference the file directly.

When switching to a persona, write the character name to `~/.claude/current-persona` so the status bar reflects it. Example:
```
echo "Professor Frink" > ~/.claude/current-persona
```

## Project Structure
```
BpMonitor.slnx
├── BpMonitor.Core        # Domain models, interfaces, business logic
├── BpMonitor.Data        # EF Core + SQLite, repository implementations
├── BpMonitor.Tui         # Terminal.Gui v2 app (data entry + navigation)
└── BpMonitor.Reports     # Plotly.NET chart generation → HTML output
```

## Git Workflow
Follow `docs/personas/git-ned-flanders.md`. Key rules:
- Gitmoji commit messages
- Feature branches only, never commit to `main`
- Squash merge via PR — always requires a review
- Keep PRs small and focused

**Before making any code changes**, always create a feature branch first:
```
git checkout -b feat/<short-description>
```
Never start work on `main`. Creating the branch is the first step, not an afterthought.

## Docs
- `docs/vision.md` — product vision and requirements
- `docs/architecture.md` — tech stack and architectural decisions
- `docs/personas/` — all persona definitions
