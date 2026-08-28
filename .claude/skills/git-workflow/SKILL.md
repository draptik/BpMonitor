---
name: git-workflow
description: Git workflow reference — branching strategy, gitmoji + conventional commit format, PR conventions, and post-merge cleanup. Auto-loaded whenever performing git operations, creating branches, writing commits, or opening pull requests.
user-invocable: false
model: claude-haiku-4-5-20251001
---

# Git Workflow

Enforce a clean, consistent git workflow. Every change is traceable, reviewable, and revertable. No shortcuts, no exceptions.

A standalone script mirroring the commit → PR → CI → merge flow lives at
`scripts/ship-pr.sh` — use it directly (no Claude needed): stage your files
with `git add`, then run `scripts/ship-pr.sh "<gitmoji + conventional title>"`.
It opens `$EDITOR` for the PR summary, watches CI, and only merges after
explicit confirmation. Keep it in sync with this skill when the process changes.

Inside Claude, the `ship` skill (`/ship`) runs the same flow end-to-end —
commit, push, PR, CI wait, squash-merge, cleanup — without per-step
confirmation, auto-merging once CI is green.

## Branching Strategy

```text
main          ← protected, always stable, squash-merged only
└── feature/short-description
└── fix/short-description
└── chore/short-description
```

- Branch off `main` for every change
- Branch names: lowercase, hyphenated, prefixed by type (`feature/`, `fix/`, `chore/`)
- Delete branch after merge
- **Before making any code changes**, always create a feature branch first — it is the first step, not an afterthought

## Commit Message Convention (Gitmoji + Conventional Commits)

Format:

```text
<emoji> <type>[optional scope]: <short description in imperative mood>
```

This is a strict whitelist, mechanically enforced by `.husky/commit-msg` —
each row is the *only* legal pairing for that emoji, and each type has
exactly one emoji (fix's two rows are the one deliberate exception: 🔒 is a
security-flavored `fix`, always still typed `fix`). No emoji outside this
table is permitted, and an emoji may never appear next to a different type
than the one listed here — mixing them (e.g. `💄 chore`, `♻️ test`, `🔧
refactor`) is exactly the drift this table exists to prevent.

| Emoji | Code | Conventional Type | Use for |
| --- | --- | --- | --- |
| ✨ | `:sparkles:` | `feat` | New feature |
| 🐛 | `:bug:` | `fix` | Bug fix |
| 🔒 | `:lock:` | `fix` | Security fix |
| ♻️ | `:recycle:` | `refactor` | Behavior-preserving restructuring — including splitting/deduplicating test files with no assertion changes |
| ✅ | `:white_check_mark:` | `test` | Add or change what a test asserts / covers |
| 📝 | `:memo:` | `docs` | Documentation |
| 🔧 | `:wrench:` | `chore` | Configuration / tooling |
| 🗑️ | `:wastebasket:` | `chore` | Remove code or files |
| ⬆️ | `:arrow_up:` | `chore` | Upgrade dependencies |
| 🎉 | `:tada:` | `chore` | Initial commit |
| 💄 | `:lipstick:` | `style` | Visual-only change, no logic change |
| 👷 | `:construction_worker:` | `ci` | CI pipeline changes |
| ⚡️ | `:zap:` | `perf` | Performance improvement (always with the ️ variation selector — not bare ⚡) |
| 🚀 | `:rocket:` | `chore` | Deploy |

Picking the type decides the emoji, not the other way around — never choose
an emoji because it "feels right" for the change. In particular:

- Reorganizing or splitting test files without changing what they assert is
  `♻️ refactor(test)`, not `✅ test` — `✅ test` is reserved for commits that
  add or change actual coverage.
- A CSS/markup-only fix to a broken layout is still `🐛 fix`, not `💄 style`
  — `style` is for changes with no functional intent at all (e.g. a
  find-alignment tweak someone already asked for), `fix` is for correcting
  something that was wrong.

Examples:

```text
✨ feat: add blood pressure entry form
🐛 fix: fix timestamp not saving in UTC
♻️ refactor: extract reading validation into domain service
✅ test: add tests for out-of-range systolic values
♻️ refactor(test): split oversized test file, no assertion changes
👷 ci: run E2E tests in their own step
```

## Pull Request Workflow

1. Push feature branch
2. Open PR against `main`
3. **Wait for CI to pass** — run `gh pr checks <number> --watch` and do not merge until all checks are green
4. All feedback must be resolved before merge
5. Merge strategy: **Squash and merge** (one clean commit per PR on `main`)
6. After merge: run `git fetch --prune` and delete the local branch (`git branch -D <branch>`)

### PR Title and Description Format

The PR title becomes the squash-merge commit on `main` — it **must** follow the same gitmoji + conventional commits convention.

PR body:

```text
## Summary
- <bullet points describing what changed and why>
```

⛔ **NEVER include a "Generated with Claude Code" footer.**
⛔ **NEVER include a "Test plan" section.**
⛔ **Always update the PR description when new commits are pushed.**

## Rules

- NEVER commit directly to `main`
- NEVER merge before CI passes — always wait for all checks to go green (`gh pr checks <number> --watch`)
- NEVER merge if any CI step is failing — fix the build first
- NEVER use `git push --force` on `main`
- NEVER add `Co-Authored-By: Claude` trailers to commits
- NEVER commit without explicit user approval — always show the planned commit message and ask first
- Keep PRs small and focused — one concern per PR
- Write commit messages in imperative mood ("Add", not "Added" or "Adding")
- After every merge: `git fetch --prune` then `git branch -D <branch>`
- NEVER bypass `.husky/commit-msg`'s emoji/type check with `--no-verify` — fix the message instead
