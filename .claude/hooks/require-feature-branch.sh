#!/usr/bin/env bash
# PreToolUse hook for Edit/Write/NotebookEdit: blocks edits inside the repo
# while HEAD is on main. git-workflow skill requires branching off main
# before any code changes — husky's pre-commit hook only catches this at
# commit time, after the edits already happened.
set -euo pipefail

input=$(cat)
file_path=$(printf '%s' "$input" | jq -r '.tool_input.file_path // ""')

if [ -z "$file_path" ]; then
  exit 0
fi

project_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"
case "$file_path" in
  "$project_dir"/*) ;;
  *) exit 0 ;;
esac

branch=$(git -C "$project_dir" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "")

if [ "$branch" = "main" ]; then
  jq -n '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",
    permissionDecisionReason:"AGENTS.md: create a feature branch before any edits — git checkout -b <feature|fix|chore>/<desc>"}}'
fi
