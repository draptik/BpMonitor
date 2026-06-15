# Contributing to BpMonitor

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — version is pinned in `code/global.json`
- [mise](https://mise.jdx.dev/) — manages all non-dotnet linting tools (Biome, markdownlint-cli2, shellcheck, node)

## Getting started

```bash
# Install pinned tools (Biome, markdownlint-cli2, shellcheck, node)
mise install

# Restore .NET local tools (Fantomas, dotnet-ef) and dependencies
cd code
dotnet tool restore
dotnet restore

# Build and test
dotnet build
dotnet test
```

## Running locally

```bash
# Web app — served at http://localhost:5000
dotnet run --project code/BpMonitor.Web
```

## Demo data (Simpson family)

For developer onboarding and demos, a pre-built Simpson-family dataset can be
seeded into a fresh database. It covers ~5 years of readings with per-member
personalities so all features render richly out of the box:

```bash
# Remove any existing DB and start fresh with the Simpson family
rm -f code/BpMonitor.Web/bpmonitor.db*
BpMonitor__SeedDemoData=true dotnet run --project code/BpMonitor.Web
```

The flag is **off by default** — production databases are never touched. Seeding
only runs when the database is empty, so a second run with the flag is a no-op.

Members are seeded **unclaimed** (no password). Use the first-login flow at
`http://localhost:5000/login` to claim a member and set a password. Marge
Simpson is the admin.

## Project structure

The solution is split into focused projects under `code/` — Core domain, Data
(EF Core + SQLite), Import, Export, Charts, and Web — following Clean
Architecture. See [docs/architecture.md](docs/architecture.md) for the full
structure, dependency diagram, and tech stack.

## Linting and formatting

These mirror the jobs in `.github/workflows/ci.yml`. [Husky](https://alirezanet.github.io/Husky.Net/)
pre-commit hooks run them automatically — restore tools once to activate:

```bash
cd code && dotnet tool restore   # only needed once after cloning
```

| Tool | Command | What it checks |
| ---- | ------- | -------------- |
| Fantomas | `cd code && dotnet fantomas --check .` | F# formatting |
| Biome | `mise exec -- biome check` | JS linting |
| markdownlint | `mise exec -- markdownlint-cli2 "**/*.md"` | Markdown style |
| shellcheck | `mise exec -- shellcheck <file>.sh` | Shell scripts |

Run all non-dotnet linters at once:

```bash
mise run lint        # markdownlint + biome + shellcheck
mise run lint:md     # markdownlint only
mise run lint:js     # biome only
mise run lint:shell  # shellcheck only
```

Auto-fix where supported:

```bash
cd code && dotnet fantomas .          # reformat F#
mise exec -- biome check --write      # fix JS
```

## Testing

```bash
cd code && dotnet test
```

All code changes follow strict TDD (Red → Green → Refactor). See the "Working Rules"
section in [AGENTS.md](AGENTS.md) for the full expectations.

## Code style

F# conventions (lambda shorthand, indexer syntax, `IDisposable` rules) are documented in
the "F# Style Conventions" section of [AGENTS.md](AGENTS.md).

## Git workflow

- Branch prefixes: `feature/`, `fix/`, `chore/`
- Commit format: gitmoji + conventional commits (e.g. `✨ feat: add reading form`)
- Merge strategy: squash-merge via PR — one clean commit per PR on `main`
- Never commit directly to `main`

See [AGENTS.md](AGENTS.md) for the full git workflow rules.

## Changelog

User-facing changes belong in [`CHANGELOG.md`](CHANGELOG.md) under the
`## [Unreleased]` section. Add a bullet there as part of every PR that introduces
a user-visible change — group it under `### Added`, `### Changed`, or `### Fixed`
as appropriate. Internal-only changes (refactors, test-only, CI tweaks) do not
need a changelog entry.

When a release is cut (see below), `/cut-release` promotes the `[Unreleased]`
entries into the new version section automatically.

## Creating a release

Use the `/cut-release` skill, which walks through the full flow (preflight, change
summary, confirmation, tag, push). The quickest path is:

```bash
# inside Claude Code
/cut-release v1.2.3
```

The skill creates an **annotated** tag whose message becomes the end-user summary
displayed above the auto-generated changelog on the GitHub release page. It pauses
for confirmation before pushing anything.

If you prefer to tag manually, create an annotated tag — the message will appear
as the release summary:

```bash
git tag -a v1.2.3 -m "$(cat <<'EOF'
### Highlights

- <new feature>

### Deployment notes

- <anything the operator must know — omit if nothing actionable>
EOF
)"
git push origin v1.2.3
```

`release.yml` builds the self-contained tarball (`bpmonitor-web-linux-x64.tar.gz`)
and publishes a container image to `ghcr.io/draptik/bpmonitor-web`. Tags containing
`-` (e.g. `v1.2.3-rc1`) are automatically flagged as GitHub pre-releases, keeping
`/releases/latest` on the stable release.

## License

By contributing you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
