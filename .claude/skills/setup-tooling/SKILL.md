---
name: setup-tooling
description: Audit and set up code quality infrastructure — formatters (Fantomas), linters (markdownlint), .editorconfig, .gitattributes, .gitignore, and MSBuild build properties.
model: claude-haiku-4-5-20251001
---

Establish and maintain the project's code quality infrastructure. Standards are not suggestions — they are the rules, and the rules will be followed.

## Responsibilities

- `.editorconfig` — indentation, line endings, charset, trailing whitespace, final newline
- `.gitattributes` — line ending normalization, binary file markers
- `.gitignore` — build artifacts, IDE files, secrets
- `Fantomas` — the F# code formatter; configured via `.fantomas-config.json` or `fantomas` section in `Directory.Build.props`
- `markdownlint-cli2` — markdown linting; configured via `.markdownlint-cli2.yaml`
- `Directory.Build.props` — solution-wide MSBuild properties (treat warnings as errors, analyzer severity)
- `Directory.Packages.props` — centralized NuGet package version management

## Rules

- Every setting must have a reason — no cargo-culting
- Warnings are errors in CI (`TreatWarningsAsErrors = true`)
- Line endings: **LF** everywhere (`.gitattributes` enforces this)
- Indentation: **2 spaces** for F#, FSX, FSI, fsproj, JSON, YAML, XML — **4 spaces** for C# (if any)
- Max line length: **120 characters**
- When suppressing a lint/analyzer rule, always include a comment explaining why
- Formatting is enforced by Fantomas in the pre-commit hook and CI — do not fight the formatter

## Workflow

1. Audit the current state of tooling (what exists, what is missing)
2. Propose the changes with justification before touching any file
3. Apply incrementally — one concern at a time
4. Verify: `dotnet build` produces zero warnings after changes
5. Document any non-obvious decisions inline or in `docs/architecture.md`
