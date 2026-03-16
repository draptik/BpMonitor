---
name: git-workflow
description: Git workflow reference — branching strategy, gitmoji + conventional commit format, PR conventions, and post-merge cleanup. Auto-loaded whenever performing git operations, creating branches, writing commits, or opening pull requests.
user-invocable: false
model: claude-haiku-4-5-20251001
---

Enforce a clean, consistent git workflow. Every change is traceable, reviewable, and revertable. No shortcuts, no exceptions.

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

| Emoji | Code | Conventional Type | Use for |
| --- | --- | --- | --- |
| ✨ | `:sparkles:` | `feat` | New feature |
| 🐛 | `:bug:` | `fix` | Bug fix |
| ♻️ | `:recycle:` | `refactor` | Refactor |
| ✅ | `:white_check_mark:` | `test` | Add or update tests |
| 📝 | `:memo:` | `docs` | Documentation |
| 🔧 | `:wrench:` | `chore` | Configuration / tooling |
| 🗑️ | `:wastebasket:` | `chore` | Remove code or files |
| ⬆️ | `:arrow_up:` | `chore` | Upgrade dependencies |
| 🎉 | `:tada:` | `chore` | Initial commit |
| 🔒 | `:lock:` | `fix` | Security fix |
| 💄 | `:lipstick:` | `style` | UI / style changes |
| 🚀 | `:rocket:` | `chore` | Deploy |

Examples:

```text
✨ feat: add blood pressure entry form
🐛 fix: fix timestamp not saving in UTC
♻️ refactor: extract reading validation into domain service
✅ test: add tests for out-of-range systolic values
```

## Pull Request Workflow

1. Push feature branch
2. Open PR against `main`
3. All feedback must be resolved before merge
4. Merge strategy: **Squash and merge** (one clean commit per PR on `main`)
5. After merge: run `git fetch --prune` and delete the local branch (`git branch -D <branch>`)

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
- NEVER merge if any CI step is failing — fix the build first
- NEVER use `git push --force` on `main`
- NEVER add `Co-Authored-By: Claude` trailers to commits
- NEVER commit without explicit user approval — always show the planned commit message and ask first
- Keep PRs small and focused — one concern per PR
- Write commit messages in imperative mood ("Add", not "Added" or "Adding")
- After every merge: `git fetch --prune` then `git branch -D <branch>`
