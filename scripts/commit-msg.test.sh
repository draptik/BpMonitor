#!/usr/bin/env bash
# Tests for .husky/commit-msg (run: mise run test:scripts).
set -uo pipefail

# husky.sh is gitignored (absent in CI), so run a copy of the hook next to an empty stub.
sandbox=$(mktemp -d)
trap 'rm -rf "$sandbox"' EXIT
mkdir "$sandbox/_"
: >"$sandbox/_/husky.sh"
cp "$(dirname "$0")/../.husky/commit-msg" "$sandbox/commit-msg"
hook="$sandbox/commit-msg"
failures=0

run_hook() {
  printf '%s\n' "$1" >"$sandbox/msg"
  sh "$hook" "$sandbox/msg" >/dev/null 2>&1
}

accepts() {
  run_hook "$1"
  local status=$?
  if [ "$status" -ne 0 ]; then
    echo "FAIL: should accept '$1' (exit $status)"
    failures=$((failures + 1))
  fi
}

rejects() {
  run_hook "$2"
  local status=$?
  if [ "$status" -ne 1 ]; then
    echo "FAIL: $1: should reject '$2' (exit $status)"
    failures=$((failures + 1))
  fi
}

vs=$'️' # emoji variation selector

for prefix in \
  "✨ feat" "🚸 feat" "🌐 feat" "♿${vs} feat" "💥 feat" \
  "🐛 fix" "🔒 fix" "🩹 fix" "🚑${vs} fix" "♿${vs} fix" "🌐 fix" \
  "♻${vs} refactor" "🎨 refactor" "🚚 refactor" "🔥 refactor" \
  "✅ test" "🧪 test" "🤡 test" "♻${vs} test" \
  "📝 docs" "💡 docs" "📄 docs" \
  "💄 style" "🎨 style" "🚨 style" \
  "👷 ci" "💚 ci" \
  "⚡${vs} perf" \
  "📦${vs} build" \
  "🔧 chore" "🔨 chore" "⬆${vs} chore" "⬇${vs} chore" "📌 chore" "➕ chore" "➖ chore" \
  "🗑${vs} chore" "🔥 chore" "🙈 chore" "🚚 chore" "🔖 chore" "🚀 chore" "🎉 chore"; do
  accepts "$prefix: do something"
  accepts "${prefix}(web): do something"
done

# Bare emojis without the variation selector render at the wrong width in terminals.
for prefix in "♻ refactor" "♻ test" "⚡ perf" "♿ feat" "🚑 fix" "📦 build" "⬆ chore" "⬇ chore" "🗑 chore"; do
  rejects "missing variation selector" "$prefix: do something"
done

rejects "types emoji not offered" "🏷${vs} refactor: do something"
rejects "emoji of another type" "✨ chore: do something"
rejects "emoji of another type" "🔧 refactor: do something"
rejects "emoji of another type" "💄 chore: do something"
rejects "unknown type" "🔧 wip: do something"
rejects "no emoji" "chore: do something"

[ "$failures" -eq 0 ] && echo "All tests passed"
exit "$failures"
