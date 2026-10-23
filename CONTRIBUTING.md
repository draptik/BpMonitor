# Contributing to BpMonitor

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download) (currently a release candidate) — version is pinned in `code/global.json`; Fantomas also needs the .NET 10 runtime
- [mise](https://mise.jdx.dev/) — manages all non-dotnet tools (linters, Trivy; see `mise.toml`)

## Getting started

```bash
# Install pinned tools (see mise.toml)
mise install

# Restore .NET local tools (Fantomas, dotnet-ef) and dependencies
cd code
dotnet tool restore
dotnet restore

# Build and test
dotnet build
dotnet test
```

Before the first `dotnet test`, install the Playwright browsers the E2E tests
need (one-time, from the repo root): `mise run test:e2e-setup`.

## Running locally

```bash
# Web app — served at http://localhost:5000
dotnet run --project code/BpMonitor.Web
```

## Demo data (Simpson family)

For developer onboarding and demos, a pre-built Simpson-family dataset can be
seeded into a fresh database. It covers ~5 years of readings with per-member
personalities so all features render richly out of the box. A sixth member,
Ned Flanders, tells a Fig. 5-style story (Wegier et al. 2021, see
`docs/resources/12911_2021_Article_1598.pdf`): elevated readings, a week-long
gap, then improved control after starting medication — exercising the
/recent chart's missing-data dashed line and LOWESS trend overlay:

```bash
# Remove any existing DB and start fresh with the Simpson family
rm -f code/BpMonitor.Web/bpmonitor.db*
BpMonitor__SeedDemoData=true dotnet run --project code/BpMonitor.Web
```

The flag is **off by default** — production databases are never touched. Seeding
only runs when the database is empty, so a second run with the flag is a no-op.

Members are seeded **unclaimed** (no password). On startup the app logs a
one-time setup code for each of them (`Setup code for member … : XXXX-XXXX`).
Sign in at `http://localhost:5000/login` with a member's name, then enter their
code and choose a password. Marge Simpson is the admin.

New passwords need at least 8 characters (`BpMonitor:MinPasswordLength`).
`appsettings.Development.json` lowers that to 1, so any non-empty password works
when you run the app locally (`ASPNETCORE_ENVIRONMENT=Development`, set by
`launchSettings.json`). The app logs a warning at startup whenever the minimum is
below 8.

## Project structure

The solution is split into focused projects under `code/` — Core domain, Data
(EF Core + SQLite), Export, Charts, and Web — following Clean
Architecture. See [docs/architecture.md](docs/architecture.md) for the full
structure, dependency diagram, and tech stack.

### Vendored plotly.js

`code/BpMonitor.Web/wwwroot/plotly-*.min.js` is extracted from the embedded
resource inside the Plotly.NET NuGet package (not downloaded from a CDN), so
the chart version always matches what Plotly.NET ships. After bumping the
`Plotly.NET` package version, re-run:

```bash
dotnet restore code
dotnet fsi scripts/extract-plotly-js.fsx
```

A test in `BpMonitor.Web.Tests` (`PlotlyVendoringTests`) fails CI if the
vendored file ever drifts from the package's embedded resource.

## Linting and formatting

These mirror the CI jobs, and [Husky](https://alirezanet.github.io/Husky.Net/) pre-commit
hooks run them on staged files once `cd code && dotnet tool restore` has run. See
[docs/architecture.md](docs/architecture.md#development-tooling) for what each tool checks.

```bash
cd code && dotnet fantomas --check .  # F# formatting
mise run lint                   # all non-dotnet linters below
mise run lint:md                # markdownlint
mise run lint:spelling          # typos
mise run lint:prose             # Vale
mise run lint:js                # Biome
mise run lint:ts                # tsc checkJs
mise run lint:shell             # shellcheck
mise run lint:actions           # actionlint
mise run lint:actions-security  # zizmor
mise run lint:container         # hadolint
mise run lint:renovate          # renovate.json validation
mise run lint:links             # lychee (network; not part of `lint`)
mise run test:scripts           # tests for scripts/
```

Auto-fix where supported:

```bash
cd code && dotnet fantomas .          # reformat F#
mise exec -- biome check --write      # fix JS
```

## Testing

Run `dotnet test` from `code/`; [AGENTS.md](AGENTS.md#testing) covers running a single
project or test. All code changes follow strict TDD (Red → Green → Refactor) — see
"Working Rules" in [AGENTS.md](AGENTS.md).

### Benchmarks

`code/BpMonitor.Benchmarks` holds BenchmarkDotNet benchmarks, currently the original
LOWESS (`Lowess.Reference`) against the sliding-window one for one and five years of
readings. It is a console app, not a test project, so `dotnet test` and CI skip it:

```bash
mise run bench                  # full run, a few minutes
mise run bench -- --job short   # quick check
```

Results land in `code/BenchmarkDotNet.Artifacts/` (git-ignored).

## Code style

F# conventions (lambda shorthand, indexer syntax, `IDisposable` rules) are documented in
the "F# Style Conventions" section of [AGENTS.md](AGENTS.md).

## Git workflow

- Branch prefixes: `feat/`, `fix/`, `chore/` (the Husky pre-commit hook and `/ship` reject others)
- Commit format: gitmoji + conventional commits (e.g. `✨ feat: add reading form`)
- Merge strategy: squash-merge via PR — one clean commit per PR on `main`
- `main` requires all six CI checks to pass (repository ruleset); `/ship` queues the
  merge with `gh pr merge --auto --squash`, so GitHub merges only once they're green
- Never commit directly to `main`

See [AGENTS.md](AGENTS.md) for the full git workflow rules.

## Changelog

User-facing changes belong in [`CHANGELOG.md`](CHANGELOG.md) under the
`## [Unreleased]` section. Add a bullet there as part of every PR that introduces
a user-visible change — group it under `### Added`, `### Changed`, `### Fixed`,
`### Security`, or `### Maintenance` as appropriate. Internal-only changes
(refactors, test-only, CI tweaks) do not need a changelog entry.

Write each bullet in present tense, describing current behavior — not the
sequence of PRs that got there ("the hover label is now localized", not "the
hover label was broken, then fixed"). If a bug is introduced and fixed before
either change ever ships, it has no user-facing history: fold the fix into the
original `Added`/`Changed` bullet's description instead of adding a separate
`Fixed` one. `/cut-release` re-checks this before a release ships.

`/cut-release` promotes the `[Unreleased]` entries into the new version section.

## Creating a release

Run `/cut-release v1.2.3` inside Claude Code (or `scripts/cut-release.sh` without it). It
walks through preflight, change summary, changelog PR and tag, and pauses for confirmation
before pushing anything. The annotated tag's message becomes the end-user summary at the top
of the GitHub release.

Tagging by hand works too, but the tag must be annotated and created with
`--cleanup=verbatim`, otherwise git strips the summary's `###` heading as a comment.

`release.yml` builds the self-contained tarball (`bpmonitor-web-linux-x64.tar.gz`)
and publishes a container image to `ghcr.io/draptik/bpmonitor-web`, but only after
Trivy finds no fixable CRITICAL/HIGH CVEs in it. If the scan blocks on a base-image CVE
only Microsoft can fix, add a path-scoped, expiring entry to `.trivyignore.yaml` via a PR
and tag a new patch version — re-running the job uses the tagged commit's ignore file. Tags
containing `-` (e.g. `v1.2.3-rc1`) become pre-releases, so `/releases/latest` stays stable.

The GitHub release body is the tag annotation (the end-user summary) followed by
notes from `scripts/release-notes.sh`: commits since the previous tag (the previous stable
one, for a stable release) grouped under the `CHANGELOG.md` headings, with dependency bumps
collapsed into a `<details>` block.

## License

By contributing you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
