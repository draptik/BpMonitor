#!/usr/bin/env bash
# Stop hook: reminds to run /verify-frontend when frontend files changed
# since the last nudge. AGENTS.md says any wwwroot/Views/page-handler
# change should be verified via a real browser before reporting complete;
# this catches the case where that step gets skipped.
set -euo pipefail

project_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$project_dir"

frontend_paths=("code/BpMonitor.Web/wwwroot" "code/BpMonitor.Web/Views")

changed=""
for p in "${frontend_paths[@]}"; do
  if [ -d "$p" ]; then
    while IFS= read -r f; do
      changed="$changed$f:$(stat -c %Y "$f" 2>/dev/null || echo 0)|"
    done < <(git status --porcelain -- "$p" 2>/dev/null | awk '{print $NF}')
    branch_changed=$(git diff --name-only main...HEAD -- "$p" 2>/dev/null || true)
    for f in $branch_changed; do
      changed="$changed$f:$(stat -c %Y "$f" 2>/dev/null || echo 0)|"
    done
  fi
done

if [ -z "$changed" ]; then
  exit 0
fi

hash=$(printf '%s' "$changed" | sha256sum | cut -d' ' -f1)
stamp_file=".git/claude-verify-frontend"
previous=""
[ -f "$stamp_file" ] && previous=$(cat "$stamp_file")

if [ "$hash" = "$previous" ]; then
  exit 0
fi

printf '%s' "$hash" > "$stamp_file"
jq -n '{decision:"block",reason:"AGENTS.md: frontend files changed — run /verify-frontend before reporting this complete."}'
