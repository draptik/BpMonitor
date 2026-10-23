#!/usr/bin/env bash
# Tests for cut-release.sh against a throwaway repo and stub gh/mise (run: mise run test:scripts).
set -uo pipefail

release="$(cd "$(dirname "$0")" && pwd)/cut-release.sh"
failures=0

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Stub gh reports a green CI run on main; stub mise does nothing.
mkdir "$work/bin"
cat >"$work/bin/gh" <<'EOF'
#!/usr/bin/env bash
case "$*" in *conclusion*) echo success ;; esac
EOF
printf '#!/usr/bin/env bash\n' >"$work/bin/mise"
chmod +x "$work/bin/gh" "$work/bin/mise"

git init -q --bare "$work/origin.git"
git clone -q "$work/origin.git" "$work/repo" 2>/dev/null
cd "$work/repo" || exit 1
git checkout -q -b main
cat >CHANGELOG.md <<'EOF'
## [Unreleased]

### Added

- something

[Unreleased]: https://github.com/draptik/BpMonitor/compare/v1.0.0...HEAD
EOF
git add CHANGELOG.md
git -c user.name=t -c user.email=t@t commit -q -m init
git tag v1.0.0
git push -q origin main

# No terminal for the editor, so it fails just like an editor quit with :cq.
printf 'v1.1.0\ny\nn\n' | PATH="$work/bin:$PATH" EDITOR=false "$release" prepare >/dev/null 2>&1

if git rev-parse -q --verify chore/changelog-1.1.0 >/dev/null; then
  echo "FAIL: failed editor (changelog branch was created)"
  failures=$((failures + 1))
fi

[ "$failures" -eq 0 ] && echo "All tests passed"
exit "$failures"
