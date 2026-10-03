#!/usr/bin/env bash
# Push, open a PR, queue a squash auto-merge, wait for CI, clean up (run with --help).

set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  git add <files>   # stage exactly what you want committed
  scripts/ship-pr.sh "<gitmoji + conventional commit/PR title>"

  scripts/ship-pr.sh --yes --body-file <file> ["<title>"]
    Non-interactive (used by the `ship` skill): never commits, takes the PR
    body from <file>, skips confirm prompts. The title defaults to the commit
    subject when the branch has exactly one commit.

GitHub merges only once every required check has passed (--auto), so this
never merges on red. Re-running resumes from whichever step is next.
EOF
}

yes=false
body_file=""
title=""
while [ $# -gt 0 ]; do
  case "$1" in
    -h | --help)
      usage
      exit 0
      ;;
    --yes) yes=true ;;
    --body-file)
      body_file=${2:-}
      shift
      ;;
    -*)
      echo "Unknown option: $1" >&2
      exit 1
      ;;
    *) title=$1 ;;
  esac
  shift
done

branch=$(git branch --show-current)
if [ "$branch" = "main" ]; then
  echo "Error: never commit directly to main — create a feat/fix/chore branch first" >&2
  exit 1
fi

case "$branch" in
  feat/* | fix/* | chore/*) ;;
  *) echo "Warning: branch '$branch' doesn't match feat/|fix/|chore/ convention" >&2 ;;
esac

confirm() {
  $yes && return 0
  local answer
  read -r -p "$1 [y/N] " answer
  [ "$answer" = "y" ] || [ "$answer" = "Y" ]
}

commit_if_needed() {
  git fetch origin main -q
  if [ -n "$(git diff --cached --name-only)" ]; then
    if $yes; then
      echo "Error: staged changes — --yes never commits; commit them first" >&2
      exit 1
    fi
    [ -n "$title" ] || {
      echo "Error: a title is required to commit staged changes" >&2
      exit 1
    }
    echo "== Staged changes =="
    git diff --cached --stat
    echo ""
    confirm "Commit with message \"$title\"?" || {
      echo "Aborted."
      exit 1
    }
    git commit -m "$title"
  elif [ -n "$(git status --short --untracked-files=no)" ]; then
    echo "Error: there are unstaged changes — commit or stash them first" >&2
    git status --short --untracked-files=no
    exit 1
  elif [ -z "$(git log origin/main..HEAD --oneline 2>/dev/null)" ]; then
    echo "Error: nothing staged and no commits ahead of origin/main — nothing to ship" >&2
    exit 1
  else
    echo "Nothing staged — branch already has commits, continuing."
  fi
}

default_title() {
  [ -n "$title" ] && return
  if [ "$(git rev-list --count origin/main..HEAD)" = "1" ]; then
    title=$(git log -1 --format=%s)
  else
    echo "Error: branch has several commits — pass a PR title" >&2
    exit 1
  fi
}

push_branch() {
  git push -q -u origin "$branch"
}

pr_number_for_branch() {
  gh pr list --head "$branch" --state open --json number --jq '.[0].number // empty'
}

read_body() {
  if [ -n "$body_file" ]; then
    cat "$body_file"
    return
  fi
  if $yes; then
    echo "Error: --yes needs --body-file to open a PR" >&2
    exit 1
  fi
  local tmp
  tmp=$(mktemp)
  cat >"$tmp" <<'EOF'
# Write the PR Summary below this line, then save and exit.
# Summary section only — NEVER add a "Test plan" section or a
# "Generated with Claude Code" footer. Lines starting with '# ' are stripped.
#
## Summary
-
EOF
  "${EDITOR:-vi}" "$tmp" >/dev/tty </dev/tty
  grep -v '^# ' "$tmp" | grep -v '^#$' | sed -e '/./,$!d' -e '$ { /^$/d }'
  rm -f "$tmp"
}

# Prints only the PR number on stdout; everything else goes to stderr.
create_pr() {
  local existing
  existing=$(pr_number_for_branch)
  if [ -n "$existing" ]; then
    echo "PR #$existing already open for $branch." >&2
    echo "$existing"
    return
  fi

  default_title
  local body
  body=$(read_body)
  if [ -z "$(echo "$body" | tr -d '[:space:]')" ]; then
    echo "Error: PR body is empty — aborting" >&2
    exit 1
  fi

  gh pr create --title "$title" --body "$body" >&2
  pr_number_for_branch
}

queue_merge() {
  local pr=$1
  confirm "Queue squash auto-merge for PR #$pr once CI is green?" || {
    echo "Left open — merge manually when ready."
    exit 0
  }
  # --auto: GitHub merges only once every required check has passed.
  gh pr merge "$pr" --auto --squash
}

watch_ci() {
  local pr=$1
  echo ""
  echo "== Watching CI for PR #$pr =="
  # Checks take a few seconds to register after a push.
  local out
  for _ in $(seq 1 12); do
    out=$(gh pr checks "$pr" 2>&1 || true)
    [[ $out == *"no checks reported"* ]] || break
    sleep 5
  done
  if ! gh pr checks "$pr" --watch --fail-fast; then
    gh pr merge "$pr" --disable-auto || true
    echo "" >&2
    echo "Error: CI failed on PR #$pr — auto-merge cancelled. Fix and re-run." >&2
    exit 1
  fi
}

wait_for_merge() {
  local pr=$1 state=""
  for _ in $(seq 1 12); do
    state=$(gh pr view "$pr" --json state --jq .state)
    [ "$state" = "MERGED" ] && return 0
    sleep 5
  done
  echo "PR #$pr is $state, not MERGED — auto-merge is still queued; skipping cleanup." >&2
  exit 1
}

cleanup() {
  git checkout -q main
  git pull -q
  git fetch -q --prune
  git branch -D "$branch"
  echo "Merged and cleaned up local branch '$branch'."
}

commit_if_needed
push_branch
pr=$(create_pr)
[ -n "$pr" ] || {
  echo "Error: could not determine PR number" >&2
  exit 1
}
queue_merge "$pr"
watch_ci "$pr"
wait_for_merge "$pr"
cleanup
