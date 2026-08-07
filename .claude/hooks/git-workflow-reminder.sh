#!/usr/bin/env bash
# PreToolUse hook for Bash(git *) / Bash(gh *): reminds Claude of git-workflow
# skill rules at the moment a risky git/gh subcommand is about to run, instead
# of relying on Claude to remember to invoke the skill itself.
set -euo pipefail

input=$(cat)
cmd=$(printf '%s' "$input" | jq -r '.tool_input.command // ""')

reminder=""
case "$cmd" in
  *"git checkout -b"*|*"git switch -c"*)
    reminder="git-workflow skill: creating a branch is the FIRST step before any code changes. Confirm you branched off main, and the name is lowercase-hyphenated with a feature/fix/chore prefix."
    ;;
  *"git commit"*)
    reminder="git-workflow skill: commit message must be gitmoji + conventional commits, imperative mood. NEVER add a Co-Authored-By trailer. Only commit after showing the message and getting explicit user approval."
    ;;
  *"git push"*)
    reminder="git-workflow skill: never force-push to main. Push only the feature branch."
    ;;
  *"gh pr create"*)
    reminder="git-workflow skill: PR title must be gitmoji + conventional commits (it becomes the squash-merge commit). PR body is a Summary section only — no Test plan section, no Generated-with-Claude-Code footer."
    preflight=""
    if ! git diff --name-only main...HEAD 2>/dev/null | grep -q "^CHANGELOG.md$"; then
      preflight="$preflight CHANGELOG.md has no changes on this branch — add an [Unreleased] bullet if this is user-facing."
    fi
    if git diff --name-only --diff-filter=A main...HEAD 2>/dev/null | grep -q "\.fsproj$"; then
      touched=$(git diff --name-only main...HEAD 2>/dev/null)
      if ! printf '%s' "$touched" | grep -q "^docs/architecture.md$" || ! printf '%s' "$touched" | grep -q "^AGENTS.md$"; then
        preflight="$preflight A new project was added — update both docs/architecture.md and AGENTS.md in this branch."
      fi
    fi
    if [ -n "$preflight" ]; then
      reminder="$reminder$preflight"
    fi
    ;;
  *"gh pr merge"*)
    reminder="git-workflow skill: do not merge until \`gh pr checks <number> --watch\` shows all checks green. Squash-merge only."
    ;;
  *"git branch -D"*)
    reminder="git-workflow skill: only delete a branch after it's merged — run \`git fetch --prune\` first."
    ;;
esac

if [ -n "$reminder" ]; then
  jq -n --arg ctx "$reminder" '{hookSpecificOutput:{hookEventName:"PreToolUse",additionalContext:$ctx}}'
fi
