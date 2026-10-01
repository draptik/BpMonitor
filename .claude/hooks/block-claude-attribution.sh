#!/usr/bin/env bash
# PreToolUse hook for Bash(git *) / Bash(gh *): backstop that hard-blocks Claude attribution/session links from reaching a commit or PR.
set -euo pipefail

input=$(cat)
cmd=$(printf '%s' "$input" | jq -r '.tool_input.command // ""')

deny() {
  jq -n --arg reason "$1" '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$reason}}'
  exit 0
}

case "$cmd" in
  *"git commit"*)
    if printf '%s' "$cmd" | grep -qi "co-authored-by"; then
      deny "git-workflow skill: NEVER add a Co-Authored-By trailer to commits — no Claude attribution, no exceptions."
    fi
    if printf '%s' "$cmd" | grep -qi "claude-session"; then
      deny "git-workflow skill: NEVER add a Claude-Session trailer/link to commits — no session IDs, no exceptions."
    fi
    ;;
  *"gh pr create"*|*"gh pr edit"*)
    body=""
    if printf '%s' "$cmd" | grep -qi -- "--body-file"; then
      body_file=$(printf '%s' "$cmd" | grep -oP -- '--body-file[= ]\K\S+' || true)
      if [ -n "$body_file" ] && [ -f "$body_file" ]; then
        body=$(cat "$body_file")
      fi
    fi
    combined="$cmd
$body"
    if printf '%s' "$combined" | grep -qiE "generated with claude code|🤖|## Test plan|claude-session|claude\.ai/code/session"; then
      deny "git-workflow skill: PR body is a Summary section only — no Test plan section, no Generated-with-Claude-Code footer/emoji, no Claude-Session link."
    fi
    # any heading other than "## Summary" (line-start, or first thing after --body)
    extra=$(printf '%s' "$combined" | grep -oP -- '(?:^|--body[= ]["\x27]?)\K#{1,6}\s+\S.*' | grep -vxP '## Summary\s*["\x27]?' || true)
    if [ -n "$extra" ]; then
      deny "git-workflow skill: PR body has exactly one section, ## Summary — remove: $(printf '%s' "$extra" | head -1)"
    fi
    ;;
esac
