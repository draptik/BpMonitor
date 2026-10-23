#!/usr/bin/env bash
# Tests for ship-pr.sh against a throwaway repo and a stub gh (run: mise run test:scripts).
set -uo pipefail

ship="$(cd "$(dirname "$0")" && pwd)/ship-pr.sh"
failures=0

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Stub gh: logs every call, reports no open PR.
mkdir "$work/bin"
cat >"$work/bin/gh" <<'EOF'
#!/usr/bin/env bash
echo "gh $*" >>"$GH_LOG"
EOF
chmod +x "$work/bin/gh"
export GH_LOG="$work/gh.log"

git init -q --bare "$work/origin.git"
git clone -q "$work/origin.git" "$work/repo" 2>/dev/null
cd "$work/repo" || exit 1
git -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
git push -q origin HEAD:main
git checkout -q -b chore/x
git -c user.name=t -c user.email=t@t commit -q --allow-empty -m "✅ test: x"

printf '## Summary\n\n- change\n\n🤖 Generated with [Claude Code](https://claude.com/claude-code)\n' >"$work/body.md"

PATH="$work/bin:$PATH" "$ship" --yes --body-file "$work/body.md" >/dev/null 2>&1
status=$?

if [ "$status" -eq 0 ]; then
  echo "FAIL: rejected body (exit 0, expected non-zero)"
  failures=$((failures + 1))
fi
if grep -q "^gh pr create" "$GH_LOG" 2>/dev/null; then
  echo "FAIL: rejected body (gh pr create was called)"
  failures=$((failures + 1))
fi

[ "$failures" -eq 0 ] && echo "All tests passed"
exit "$failures"
