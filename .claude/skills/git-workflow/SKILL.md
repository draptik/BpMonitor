---
name: git-workflow
description: Git workflow reference — branching strategy, gitmoji + conventional commit format, PR conventions, and post-merge cleanup. Auto-loaded whenever performing git operations, creating branches, writing commits, or opening pull requests.
user-invocable: false
---

# Git Workflow

Enforce a clean, consistent git workflow. Every change is traceable, reviewable, and revertable. No shortcuts, no exceptions.

A standalone script mirroring the commit → PR → CI → merge flow lives at
`scripts/ship-pr.sh` — use it directly (no Claude needed): stage your files
with `git add`, then run `scripts/ship-pr.sh "<gitmoji + conventional title>"`.
It opens `$EDITOR` for the PR summary, watches CI, and only merges after
explicit confirmation. Keep it in sync with this skill when the process changes.

Inside Claude, the `ship` skill (`/ship`) runs the rest of the flow
end-to-end — push, PR, CI wait, squash-merge, cleanup — without per-step
confirmation, auto-merging once CI is green. It never commits: commit first
(with the message approved), since it aborts on uncommitted changes.

## Branching Strategy

```text
main          ← protected, always stable, squash-merged only
└── feat/short-description
└── fix/short-description
└── chore/short-description
```

- Branch off `main` for every change
- Branch names: lowercase, hyphenated, prefixed by type (`feat/`, `fix/`, `chore/`; other prefixes are rejected by the pre-commit hook, so use `chore/` for refactor/docs/test/ci work). Renovate's `renovate/` branches are also accepted, so a failing Renovate PR can be fixed in place (add commits on top; `ship-pr.sh` does not force-push a rebased branch)
- Delete branch after merge
- **Before making any code changes**, always create a feature branch first — it is the first step, not an afterthought

## Commit Message Convention (Gitmoji + Conventional Commits)

Format:

```text
<emoji> <type>[optional scope]: <short description in imperative mood>
```

The type is the real decision: `scripts/release-notes.sh` groups entries by
type and ignores the emoji. The emoji is a hint for readers and must come from
that type's rows below. `.husky/commit-msg` enforces the pairs byte-for-byte,
so an emoji that is used next to a different type (e.g. `✨ chore`,
`🔧 refactor`, `💄 chore`) is rejected. An emoji that is missing its variation
selector (U+FE0F) is rejected too: a bare `♻`, `⚡` or `🗑` renders at the wrong
width in terminals.

| Type | Emoji | Code | Use for |
| --- | --- | --- | --- |
| `feat` | ✨ | `:sparkles:` | New feature (default) |
| `feat` | 🚸 | `:children_crossing:` | Usability improvement |
| `feat` | 🌐 | `:globe_with_meridians:` | Translation / localization |
| `feat` | ♿️ | `:wheelchair:` | Accessibility improvement |
| `feat` | 💥 | `:boom:` | Breaking change |
| `fix` | 🐛 | `:bug:` | Bug fix (default) |
| `fix` | 🔒 | `:lock:` | Security fix |
| `fix` | 🩹 | `:adhesive_bandage:` | Small, non-critical fix |
| `fix` | 🚑️ | `:ambulance:` | Critical hotfix |
| `fix` | ♿️ | `:wheelchair:` | Accessibility fix |
| `fix` | 🌐 | `:globe_with_meridians:` | Translation fix |
| `refactor` | ♻️ | `:recycle:` | Behavior-preserving restructuring (default) |
| `refactor` | 🎨 | `:art:` | Improve code structure / format |
| `refactor` | 🚚 | `:truck:` | Move or rename files |
| `refactor` | 🔥 | `:fire:` | Remove code |
| `test` | ✅ | `:white_check_mark:` | Add or change what a test asserts / covers (default) |
| `test` | 🧪 | `:test_tube:` | Add a failing test |
| `test` | 🤡 | `:clown_face:` | Mocks / fakes |
| `test` | ♻️ | `:recycle:` | Restructure tests without changing assertions |
| `docs` | 📝 | `:memo:` | Documentation (default) |
| `docs` | 💡 | `:bulb:` | Code comments |
| `docs` | 📄 | `:page_facing_up:` | License |
| `style` | 💄 | `:lipstick:` | Visual-only change, no logic change (default) |
| `style` | 🎨 | `:art:` | Formatting |
| `style` | 🚨 | `:rotating_light:` | Fix linter warnings |
| `ci` | 👷 | `:construction_worker:` | CI pipeline changes (default) |
| `ci` | 💚 | `:green_heart:` | Fix a failing CI build |
| `perf` | ⚡️ | `:zap:` | Performance improvement (default) |
| `build` | 📦️ | `:package:` | Build system / packaging (default) |
| `chore` | 🔧 | `:wrench:` | Configuration / tooling (default) |
| `chore` | 🔨 | `:hammer:` | Dev scripts |
| `chore` | ⬆️ | `:arrow_up:` | Upgrade dependencies |
| `chore` | ⬇️ | `:arrow_down:` | Downgrade dependencies |
| `chore` | 📌 | `:pushpin:` | Pin dependencies |
| `chore` | ➕ | `:heavy_plus_sign:` | Add a dependency |
| `chore` | ➖ | `:heavy_minus_sign:` | Remove a dependency |
| `chore` | 🗑️ | `:wastebasket:` | Remove code or files |
| `chore` | 🔥 | `:fire:` | Remove code or files |
| `chore` | 🙈 | `:see_no_evil:` | `.gitignore` changes |
| `chore` | 🚚 | `:truck:` | Move or rename files |
| `chore` | 🔖 | `:bookmark:` | Release / version tag |
| `chore` | 🚀 | `:rocket:` | Deploy |
| `chore` | 🎉 | `:tada:` | Initial commit |

Pick the type first, then an emoji from its rows. When in doubt, use the
type's default. In particular:

- Reorganizing or splitting test files without changing what they assert is
  `♻️ refactor(test)` or `♻️ test`, not `✅ test` — `✅` is reserved for
  commits that add or change actual coverage.
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
⛔ **NEVER add any heading besides `## Summary`** (no "Related", "Notes", "Context", …) *(enforced by hook)*.
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
