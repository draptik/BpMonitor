# Contributing to BpMonitor

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — version is pinned in `code/global.json`
- [mise](https://mise.jdx.dev/) — manages the JS tooling (Biome)

## Getting started

```bash
# Install pinned tools (Biome)
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
| markdownlint | `npx markdownlint-cli2 "**/*.md"` | Markdown style |
| shellcheck | `shellcheck <file>.sh` | Shell scripts |

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

## Creating a release

Push a version tag on `main` to trigger the release workflow:

```bash
git tag v1.2.3
git push origin v1.2.3
```

`release.yml` builds the self-contained tarball (`bpmonitor-web-linux-x64.tar.gz`)
and publishes a container image to `ghcr.io/draptik/bpmonitor-web`. Tags containing
`-` (e.g. `v1.2.3-rc1`) are automatically flagged as GitHub pre-releases, keeping
`/releases/latest` on the stable release.

## License

By contributing you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
