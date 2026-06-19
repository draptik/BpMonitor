#!/usr/bin/env bash
# Commit staged changes, open a PR, wait for CI, and squash-merge — mirrors
# .claude/skills/git-workflow/SKILL.md. For use without Claude (e.g. out of credits).
#
# Usage:
#   git add <files>                 # stage exactly what you want committed
#   scripts/ship-pr.sh "<gitmoji + conventional commit/PR title>"
#
# Stops and reports if CI fails — never merges on red. Re-run is safe: it
# resumes from whichever step is next (commit, push, PR, or merge) based on
# what's already done.

set -euo pipefail

title=${1:-}
if [ -z "$title" ]; then
  echo "Usage: $0 \"<gitmoji + conventional commit/PR title>\"" >&2
  exit 1
fi

branch=$(git branch --show-current)
if [ "$branch" = "main" ]; then
  echo "Error: never commit directly to main — create a feature/fix/chore branch first" >&2
  exit 1
fi

case "$branch" in
  feature/* | fix/* | chore/*) ;;
  *) echo "Warning: branch '$branch' doesn't match feature/|fix/|chore/ convention" >&2 ;;
esac

commit_if_needed() {
  git fetch origin main -q
  if [ -n "$(git diff --cached --name-only)" ]; then
    echo "== Staged changes =="
    git diff --cached --stat
    echo ""
    read -r -p "Commit with message \"$title\"? [y/N] " confirm
    [ "$confirm" = "y" ] || [ "$confirm" = "Y" ] || { echo "Aborted."; exit 1; }
    git commit -m "$title"
  elif [ -n "$(git status --short)" ]; then
    echo "Error: there are unstaged changes — run 'git add <files>' first" >&2
    git status --short
    exit 1
  elif [ -z "$(git log origin/main..HEAD --oneline 2>/dev/null)" ]; then
    echo "Error: nothing staged and no commits ahead of origin/main — nothing to ship" >&2
    exit 1
  else
    echo "Nothing staged — branch already has commits, continuing."
  fi
}

push_branch() {
  git push -u origin "$branch"
}

pr_number_for_branch() {
  gh pr list --head "$branch" --state open --json number --jq '.[0].number // empty'
}

create_pr() {
  local existing
  existing=$(pr_number_for_branch)
  if [ -n "$existing" ]; then
    echo "PR #$existing already open for $branch."
    echo "$existing"
    return
  fi

  local body_file
  body_file=$(mktemp)
  cat >"$body_file" <<'EOF'
# Write the PR Summary below this line, then save and exit.
# Summary section only — NEVER add a "Test plan" section or a
# "Generated with Claude Code" footer. Lines starting with '# ' are stripped.
#
## Summary
-
EOF
  "${EDITOR:-vi}" "$body_file" >/dev/tty </dev/tty

  local body
  body=$(grep -v '^# ' "$body_file" | grep -v '^#$' | sed -e '/./,$!d' -e '$ { /^$/d }')
  rm -f "$body_file"

  if [ -z "$(echo "$body" | tr -d '[:space:]')" ]; then
    echo "Error: PR body is empty — aborting" >&2
    exit 1
  fi

  gh pr create --title "$title" --body "$body" >/dev/null
  pr_number_for_branch
}

watch_ci() {
  local pr=$1
  echo ""
  echo "== Watching CI for PR #$pr =="
  if gh pr checks "$pr" --watch; then
    return 0
  else
    echo "" >&2
    echo "Error: CI failed (or is still pending) on PR #$pr — fix and re-run, do not merge." >&2
    return 1
  fi
}

merge_pr() {
  local pr=$1
  echo ""
  read -r -p "All checks green. Squash-merge PR #$pr? [y/N] " confirm
  [ "$confirm" = "y" ] || [ "$confirm" = "Y" ] || { echo "Left open — merge manually when ready."; exit 0; }

  gh pr merge "$pr" --squash

  git checkout main
  git pull
  git fetch --prune
  git branch -D "$branch"
  echo "Merged and cleaned up local branch '$branch'."
}

commit_if_needed
push_branch
pr=$(create_pr)
[ -n "$pr" ] || { echo "Error: could not determine PR number" >&2; exit 1; }
watch_ci "$pr"
merge_pr "$pr"
