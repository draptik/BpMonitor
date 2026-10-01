---
name: ship
description: Commit staged changes, push, open a PR, wait for CI, and squash-merge once green — the full "commit, push, open PR, merge once CI passes" flow in one invocation. Invoke when the user wants to ship the current branch's changes end-to-end.
model: haiku
context: fork
agent: general-purpose
---

# Ship

**Your task: ship the current branch now.** Execute the Preflight and Steps
below immediately using your tools — do not wait for further instructions.
$ARGUMENTS

Run the full commit → push → PR → CI → merge → cleanup flow for the current
feature branch, per the `git-workflow` skill. This skill performs every step
itself — it does not stop mid-flow to ask "should I continue?" — and it
refuses outright (no override) to merge while CI is red. It runs as a Haiku
subagent (`context: fork`) with no conversation history and no way to ask
questions, so its final report must include the commit message and PR
summary it used.

A standalone script mirroring this flow lives at `scripts/ship-pr.sh` — use it
directly (no Claude needed): `scripts/ship-pr.sh "<gitmoji + conventional
title>"`. Keep both in sync when changing the process.

## Preflight

- Abort if the current branch is `main` — never commit there.
- Abort if there are no staged changes, no unstaged changes, and no commits
  ahead of `origin/main` — nothing to ship.

## Steps

1. **Commit** (skip if nothing staged and branch already has unpushed commits):
   - If there are staged changes, draft a gitmoji + conventional commit
     message from the diff (see `git-workflow` for the emoji table).
   - If there are unstaged changes alongside staged ones, stop and report
     back — never silently `git add -A`.
   - Create the commit with the drafted message.
2. **Push** the branch with `git push -u origin <branch>`.
3. **Open the PR** (skip if one is already open for this branch):
   - Title = the commit message (gitmoji + conventional).
   - Body = `## Summary` bullets drafted from the diff and commit log, and
     nothing else — no other headings or sections (the hook rejects them).
   - `gh pr create --title "<title>" --body "<body>"`.
4. **Watch CI**: `gh pr checks <number> --watch`. If any check fails, stop and
   report — do not merge, do not retry automatically.
5. **Merge**: once all checks are green, `gh pr merge <number> --squash`. Do
   not pause for a separate confirmation here — invoking this skill is the
   approval for the whole flow, conditioned on CI passing.
6. **Clean up**: `git checkout main && git pull && git fetch --prune && git
   branch -D <branch>`.

## Rules

- Never push to or merge `main` directly.
- Never merge with a failing or pending check.
- Never add a `Co-Authored-By: Claude` trailer.
- Never include any section besides `## Summary` in the PR body.
