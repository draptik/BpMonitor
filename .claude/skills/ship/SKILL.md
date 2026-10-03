---
name: ship
description: Push the already-committed branch, open a PR, wait for CI, and squash-merge once green — the full "push, open PR, merge once CI passes" flow in one invocation. Commit (with the user's approval of the message) before invoking. Invoke when the user wants to ship the current branch's commits end-to-end.
model: haiku
context: fork
agent: general-purpose
---

# Ship

**Your task: ship the current branch now.** Do the two steps below
immediately — do not wait for further instructions, and never ask for
confirmation: invoking this skill is the approval.
$ARGUMENTS

All the push → PR → auto-merge → CI → cleanup work lives in
`scripts/ship-pr.sh`; your only jobs are drafting the PR body and reporting
the script's result. You run as a Haiku subagent (`context: fork`) with no
conversation history, so you never commit — the script's `--yes` mode refuses
to, and aborts on uncommitted changes.

## Steps

1. **Draft the PR body** (skip if `gh pr list --head "$(git branch
   --show-current)" --state open` already shows a PR): read `git log
   origin/main..HEAD` and `git diff origin/main...HEAD --stat`, then write a
   `## Summary` section of bullets — and nothing else, no other headings (a
   hook rejects them).
2. **Write the body and run the script in one Bash command**, in the
   foreground, exactly once (a temp path doesn't survive between commands):

   ```bash
   f=$(mktemp) && cat >"$f" <<'BODY' && scripts/ship-pr.sh --yes --body-file "$f"
   ## Summary

   - <bullet>
   BODY
   ```

   For a branch with several commits, also pass a gitmoji + conventional
   title as the last argument (with one commit it defaults to its subject).
   Use a Bash timeout of 600000 ms; CI takes several minutes.

## Report

Report the PR number, title and summary, and the script's outcome, quoting
its last lines. The PR is merged only if the script printed `Merged and
cleaned up local branch`. If it exited non-zero, report its error verbatim.

## Rules

- Never run `git commit`, `gh pr merge`, `gh pr checks`, or any polling or
  wait loop yourself — the script does all of that.
- Never re-run the script after a CI failure; report and stop.
- Never add a `Co-Authored-By: Claude` trailer.
