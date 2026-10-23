#!/usr/bin/env bash
# Tests for check-pr-body.sh (run: mise run test:scripts).
set -uo pipefail

check="$(dirname "$0")/check-pr-body.sh"
failures=0

rejects() {
  printf '%s' "$2" | "$check" 2>/dev/null
  local status=$?
  if [ "$status" -ne 1 ]; then
    echo "FAIL: $1 (exit $status, expected 1)"
    failures=$((failures + 1))
  fi
}

accepts() {
  printf '%s' "$2" | "$check" 2>/dev/null
  local status=$?
  if [ "$status" -ne 0 ]; then
    echo "FAIL: $1 (exit $status, expected 0)"
    failures=$((failures + 1))
  fi
}

accepts "summary only" $'## Summary\n\n- change'
rejects "footer" $'## Summary\n\n- change\n\nGenerated with Claude Code'
rejects "robot emoji" $'## Summary\n\n- 🤖 change'
rejects "session link" $'## Summary\n\n- see https://claude.ai/code/session_abc'
rejects "extra heading" $'## Summary\n\n- change\n\n## Changes\n\n- more'

[ "$failures" -eq 0 ] && echo "All tests passed"
exit "$failures"
