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

1. **Get the PR body.** If the invocation text above contains a
   `## Summary` section, the caller drafted it with full context: use it
   verbatim (even if a PR is already open — the script then replaces its
   body). Otherwise, skip this step if `gh pr list --head "$(git branch
   --show-current)" --state open` already shows a PR; if not, read `git log
   origin/main..HEAD` (full messages) and the full `git diff
   origin/main...HEAD`, then write a `## Summary` section of bullets — and
   nothing else, no other headings (no hook checks this body — it goes
   straight into the squash commit). Accuracy rules:
   - Describe only what the diff shows; name the files/settings it touches.
   - Claim a reason or "before" behavior only if a commit message or the
     diff's removed lines state it — never infer one.
   - Don't call something "consistent with" or "matching" other code you
     haven't checked.
2. **Write the body and run the script in one Bash command** (if you
   skipped step 1, run `scripts/ship-pr.sh --yes` without `--body-file`), in the
   foreground, exactly once (a temp path doesn't survive between commands).
   Strip the list indentation — an indented closing `BODY` never ends the heredoc:

   ```bash
   f=$(mktemp)
   cat >"$f" <<'BODY'
   ## Summary

   - <bullet>
   BODY
   scripts/ship-pr.sh --yes --body-file "$f"
   ```

   The script appears exactly once, on the line after `BODY` — don't also
   chain it onto the `cat` line, or it runs twice (the second run starts on
   `main` after cleanup and fails the branch guard). For a branch with
   several commits, append a gitmoji + conventional title as the last
   argument (with one commit it defaults to its subject).
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
