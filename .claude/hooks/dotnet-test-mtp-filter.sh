#!/usr/bin/env bash
# PreToolUse hook for Bash: blocks VSTest-style `dotnet test ... --filter ...` (see AGENTS.md Testing).
set -euo pipefail

input=$(cat)
command=$(printf '%s' "$input" | jq -r '.tool_input.command // ""')

case "$command" in
  *dotnet*test*) ;;
  *) exit 0 ;;
esac

# Drop --filter-method occurrences first so they don't trip the bare --filter check.
stripped=${command//--filter-method/}

if printf '%s' "$stripped" | grep -qE -- '--filter\b'; then
  jq -n '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",
    permissionDecisionReason:"AGENTS.md: this repo runs tests on Microsoft.Testing.Platform, not VSTest — there is no top-level --filter. Use: dotnet test <project> -- --filter-method \"*Name*\""}}'
fi
