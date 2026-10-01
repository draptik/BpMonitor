---
name: ship
description: Push the already-committed branch, open a PR, wait for CI, and squash-merge once green — the full "push, open PR, merge once CI passes" flow in one invocation. Commit (with the user's approval of the message) before invoking. Invoke when the user wants to ship the current branch's commits end-to-end.
model: haiku
context: fork
agent: general-purpose
---

# Ship

**Your task: ship the current branch now.** Execute the Preflight and Steps
below immediately using your tools — do not wait for further instructions.
Never ask for confirmation at any point — invoking this skill is the approval.
Use the exact commands shown below — never write your own polling or wait scripts.
$ARGUMENTS

Run the full push → PR → CI → merge → cleanup flow for the current
feature branch, per the `git-workflow` skill. This skill performs every step
itself — it does not stop mid-flow to ask "should I continue?" — and it
refuses outright (no override) to merge while CI is red. It runs as a Haiku
subagent (`context: fork`) with no conversation history and no way to ask
questions, so it never commits: the user approves every commit message,
which only the main conversation can ask for. Its final report must include
the PR title and summary it used.

A standalone script mirroring this flow lives at `scripts/ship-pr.sh` — use it
directly (no Claude needed): `scripts/ship-pr.sh "<gitmoji + conventional
title>"`. Keep both in sync when changing the process — except that the
script still commits staged changes, since the user types its title.

## Preflight

- Abort if the current branch is `main` — never commit there.
- Abort and report if there are any staged or unstaged changes
  (`git diff --quiet && git diff --cached --quiet` fails) — never commit
  them; the main conversation must commit first.
- Abort if there are no commits ahead of `origin/main` — nothing to ship.

## Steps

1. **Push** the branch with `git push -u origin <branch>`.
2. **Open the PR** (skip if one is already open for this branch):
   - Title = the commit subject if the branch has one commit, otherwise a
     gitmoji + conventional title summarising them.
   - Body = `## Summary` bullets drafted from the diff and commit log, and
     nothing else — no other headings or sections (the hook rejects them).
   - `gh pr create --title "<title>" --body "<body>"`.
3. **Watch CI**: `gh pr checks <number> --watch`. If any check fails, stop and
   report — do not merge, do not retry automatically.
4. **Merge**: once all checks are green, `gh pr merge <number> --squash`. Do
   not pause for a separate confirmation here — invoking this skill is the
   approval for the whole flow, conditioned on CI passing.
5. **Clean up**: `git checkout main && git pull && git fetch --prune && git
   branch -D <branch>`.

## Rules

- Never commit — not even staged changes; abort instead.
- Never push to or merge `main` directly.
- Never merge with a failing or pending check.
- Never add a `Co-Authored-By: Claude` trailer.
- Never include any section besides `## Summary` in the PR body.
