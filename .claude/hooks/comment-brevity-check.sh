#!/usr/bin/env bash
# PostToolUse hook: flags newly-added code comments that aren't short and sweet.
set -euo pipefail

input=$(cat)
file_path=$(printf '%s' "$input" | jq -r '.tool_response.filePath // .tool_input.file_path // ""')

if [ -z "$file_path" ]; then
  exit 0
fi

case "$file_path" in
  *.md | *.MD) exit 0 ;;
esac

added=$(printf '%s' "$input" | jq -r '.tool_input.new_string // .tool_input.content // ""')

if [ -z "$added" ]; then
  exit 0
fi

# A comment-marker line: //, ///, #, --, /*, *, <!--  (leading whitespace ignored).
comment_re='^[[:space:]]*(///?|#|--|/\*|\*|<!--)'

run=0
offending=""

while IFS= read -r line; do
  if [[ "$line" =~ $comment_re ]]; then
    run=$((run + 1))
    if [ "$run" -ge 3 ] && [ -z "$offending" ]; then
      offending="$line"
    fi
    if [ "${#line}" -gt 150 ] && [ -z "$offending" ]; then
      offending="$line"
    fi
  else
    run=0
  fi
done <<<"$added"

if [ -n "$offending" ]; then
  reason="File $file_path has a comment that isn't short and sweet (a 3+ line comment block, or a single line over 150 chars): \"$offending\" — trim it to one tight line."
  jq -n --arg reason "$reason" '{decision:"block",reason:$reason,
    hookSpecificOutput:{hookEventName:"PostToolUse"}}'
fi
