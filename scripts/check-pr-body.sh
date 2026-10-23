#!/usr/bin/env bash
# Reads a PR body on stdin; exits 1 with the reason on stderr if it breaks the PR body rules.
set -euo pipefail

body=$(cat)

if printf '%s' "$body" | grep -qiE "generated with claude code|🤖|## Test plan|claude-session|claude\.ai/code/session"; then
  echo "PR body is a Summary section only — no Test plan section, no Generated-with-Claude-Code footer/emoji, no Claude-Session link." >&2
  exit 1
fi

# any heading other than "## Summary" (line-start, or first thing after --body)
extra=$(printf '%s' "$body" | grep -oP -- '(?:^|--body[= ]["\x27]?)\K#{1,6}\s+\S.*' | grep -vxP '## Summary\s*["\x27]?' || true)
if [ -n "$extra" ]; then
  echo "PR body has exactly one section, ## Summary — remove: $(printf '%s' "$extra" | head -1)" >&2
  exit 1
fi
