#!/usr/bin/env bash
# Generate categorized GitHub release notes from commits since the previous tag.
#
# PR titles here are strictly "<emoji> <type>(<scope>): <description>" (squash-merge
# gives one commit per PR), so the conventional-commit type word is a reliable,
# retroactive grouping key — no PR labels required. Sections mirror the Keep a
# Changelog headings used in CHANGELOG.md so both artifacts read as one vocabulary.
#
# Usage:
#   scripts/release-notes.sh vX.Y.Z
#
# Writes markdown to stdout. Used by .github/workflows/release.yml to build the
# GitHub release body (appended below the tag annotation).

set -euo pipefail

REPO="draptik/BpMonitor"

tag=${1:-}
if [ -z "$tag" ]; then
  echo "Usage: $0 vX.Y.Z" >&2
  exit 1
fi

prev_tag=$(git describe --tags --abbrev=0 "${tag}^" 2>/dev/null || true)
if [ -n "$prev_tag" ]; then
  range="${prev_tag}..${tag}"
else
  root=$(git rev-list --max-parents=0 HEAD | tail -1)
  range="${root}..${tag}"
fi

added=()
changed=()
fixed=()
security=()
maintenance=()
breaking=()
deps=()

while IFS= read -r subject; do
  [ -n "$subject" ] || continue
  case "$subject" in
    *"docs: update changelog for v"*) continue ;;
  esac

  # Strip a leading run of non-ASCII emoji (+ variation selectors / ZWJ) and any
  # following whitespace, e.g. "🐛 fix(web): ..." -> "fix(web): ...".
  rest=$(printf '%s' "$subject" | sed -E 's/^[^a-zA-Z0-9]+//')

  is_dep=false
  case "$subject" in
    "⬆️"*) is_dep=true ;;
  esac

  type=""
  scope=""
  bang=""
  desc="$rest"

  header="${rest%%: *}"
  if [ "$header" != "$rest" ]; then
    desc="${rest#*: }"
    case "$header" in
      *'!') bang="!"; header="${header%!}" ;;
    esac
    if [[ "$header" == *'('*')' ]]; then
      scope="${header#*(}"
      scope="${scope%)}"
      type="${header%%(*}"
    else
      type="$header"
    fi
  fi

  if [ "$scope" = "renovate" ]; then
    is_dep=true
  fi

  entry="- ${desc}"

  if [ -n "$bang" ]; then
    breaking+=("$entry")
  elif [ "$is_dep" = true ]; then
    deps+=("$entry")
  else
    case "$type" in
      feat) added+=("$entry") ;;
      fix) fixed+=("$entry") ;;
      perf | refactor | style) changed+=("$entry") ;;
      docs | test | chore | ci | build) maintenance+=("$entry") ;;
      *) maintenance+=("$entry") ;;
    esac
  fi
done < <(git log "$range" --no-merges --pretty=%s)

print_section() {
  local heading=$1
  shift
  local entries=("$@")
  if [ "${#entries[@]}" -eq 0 ]; then
    return
  fi
  echo "### ${heading}"
  echo ""
  printf '%s\n' "${entries[@]}"
  echo ""
}

if [ "${#breaking[@]}" -gt 0 ]; then
  print_section "⚠️ Breaking changes" "${breaking[@]}"
fi
print_section "✨ Added" "${added[@]}"
# No single icon: groups perf/refactor/style commits, so ♻️ (refactor-only) would misrepresent a style- or perf-only release.
print_section "Changed" "${changed[@]}"
print_section "🐛 Fixed" "${fixed[@]}"
print_section "🔒 Security" "${security[@]}"
print_section "🔧 Maintenance" "${maintenance[@]}"

if [ "${#deps[@]}" -gt 0 ]; then
  echo "<details><summary>⬆️ Dependency updates (${#deps[@]})</summary>"
  echo ""
  printf '%s\n' "${deps[@]}"
  echo ""
  echo "</details>"
  echo ""
fi

if [ -n "$prev_tag" ]; then
  echo "**Full Changelog**: https://github.com/${REPO}/compare/${prev_tag}...${tag}"
fi
