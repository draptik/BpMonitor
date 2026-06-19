#!/usr/bin/env bash
# Cut a BpMonitor release without Claude — mirrors .claude/skills/cut-release/SKILL.md.
#
# Usage:
#   scripts/cut-release.sh prepare [vX.Y.Z]   # preflight, pick version, edit summary,
#                                              # update CHANGELOG.md, open a PR
#   scripts/cut-release.sh tag vX.Y.Z         # after the changelog PR is merged: tag + push
#
# "prepare" stops once the changelog PR is open — watch CI and merge it yourself,
# then run "tag" with the same version to finish the release.

set -euo pipefail

REPO="draptik/BpMonitor"

usage() {
  echo "Usage: $0 prepare [vX.Y.Z] | $0 tag vX.Y.Z" >&2
  exit 1
}

[ $# -ge 1 ] || usage
cmd=$1
shift

require_clean_main() {
  local branch
  branch=$(git branch --show-current)
  if [ "$branch" != "main" ]; then
    echo "Error: must be on main (currently on $branch)" >&2
    exit 1
  fi
  if [ -n "$(git status --short)" ]; then
    echo "Error: working tree is not clean" >&2
    git status --short
    exit 1
  fi
}

preflight() {
  echo "== Preflight =="
  require_clean_main

  git fetch origin main
  if [ -n "$(git log HEAD..origin/main --oneline)" ]; then
    echo "Error: local main is behind origin/main — pull first" >&2
    exit 1
  fi

  echo "Latest CI runs on main:"
  gh run list --branch main --limit 3

  local status
  status=$(gh run list --branch main --limit 1 --json conclusion --jq '.[0].conclusion')
  if [ "$status" != "success" ]; then
    echo "Error: latest CI run on main did not succeed (conclusion: $status)" >&2
    exit 1
  fi
}

bump() {
  # bump X.Y.Z patch|minor|major -> X.Y.Z
  local version=$1 part=$2
  local major minor patch
  IFS='.' read -r major minor patch <<<"${version#v}"
  case "$part" in
    patch) patch=$((patch + 1)) ;;
    minor) minor=$((minor + 1)); patch=0 ;;
    major) major=$((major + 1)); minor=0; patch=0 ;;
  esac
  echo "v${major}.${minor}.${patch}"
}

choose_version() {
  local last_tag=$1 preselect=${2:-}
  local p m j
  p=$(bump "$last_tag" patch)
  m=$(bump "$last_tag" minor)
  j=$(bump "$last_tag" major)

  echo "Current version: $last_tag" >&2
  echo "" >&2
  printf '%-12s %-10s %s\n' "patch" "$p" "bug fixes only" >&2
  printf '%-12s %-10s %s\n' "minor" "$m" "new user-facing features" >&2
  printf '%-12s %-10s %s\n' "major" "$j" "breaking changes" >&2
  printf '%-12s %-10s %s\n' "pre-release" "${m}-rc1" "release candidate" >&2
  echo "" >&2

  local prompt="Enter version"
  if [ -n "$preselect" ]; then
    prompt="$prompt [$preselect]"
  fi
  read -r -p "$prompt: " answer
  echo "${answer:-$preselect}"
}

unreleased_body() {
  # current ## [Unreleased] section body (between its heading and the next "## [")
  awk '
    /^## \[Unreleased\]/ { found=1; next }
    found && /^## \[/ { exit }
    found { print }
  ' CHANGELOG.md | sed -e '/./,$!d'
}

edit_summary() {
  local last_tag=$1
  local existing
  existing=$(unreleased_body)
  local guide
  guide=$(mktemp)
  {
    echo "# Edit the release notes below, grouped under ### Added / ### Changed / ### Fixed."
    echo "# Pre-filled from the current ## [Unreleased] section (PRs add bullets there as they"
    echo "# land). Trim anything that isn't user/operator-facing. Lines starting with '#' are"
    echo "# stripped; save and exit when done."
    echo "#"
    echo "# Commits since ${last_tag} (for reference, in case Unreleased is incomplete):"
    git log "${last_tag}..HEAD" --oneline | sed 's/^/# /'
    echo "#"
    if [ -n "$existing" ]; then
      echo "$existing"
    else
      echo "### Added"
      echo ""
      echo "- "
    fi
  } >"$guide"

  "${EDITOR:-vi}" "$guide" >/dev/tty </dev/tty
  grep -v '^#' "$guide" | sed -e '/./,$!d' -e '$ { /^$/d }'
  rm -f "$guide"
}

# Insert a new version section right after ## [Unreleased] (clearing its body),
# and update the [Unreleased]/[X.Y.Z] compare links at the bottom of the file.
update_changelog() {
  local changelog=$1 v=$2 today=$3 prev_tag=$4 new_tag=$5 body=$6
  local body_file
  body_file=$(mktemp)
  printf '%s\n' "$body" >"$body_file"

  local old_link="[Unreleased]: https://github.com/${REPO}/compare/${prev_tag}...HEAD"
  if ! grep -qF "$old_link" "$changelog"; then
    echo "Error: could not find expected link line in CHANGELOG.md:" >&2
    echo "  $old_link" >&2
    rm -f "$body_file"
    exit 1
  fi

  local tmp
  tmp=$(mktemp)
  awk -v ver="$v" -v today="$today" -v body_file="$body_file" -v old_link="$old_link" \
      -v repo="$REPO" -v prev_tag="$prev_tag" -v new_tag="$new_tag" '
    BEGIN {
      body = ""
      while ((getline line < body_file) > 0) {
        body = body (body == "" ? "" : "\n") line
      }
    }
    /^## \[Unreleased\]/ {
      print
      print ""
      print "## [" ver "] - " today
      print ""
      print body
      skip = 1
      next
    }
    skip && /^## \[/ { skip = 0; print "" }
    skip { next }
    $0 == old_link {
      print "[Unreleased]: https://github.com/" repo "/compare/" new_tag "...HEAD"
      print "[" ver "]: https://github.com/" repo "/compare/" prev_tag "..." new_tag
      next
    }
    { print }
  ' "$changelog" >"$tmp"

  mv "$tmp" "$changelog"
  rm -f "$body_file"
}

cmd_prepare() {
  local preselect=${1:-}
  preflight

  local last_tag
  last_tag=$(git tag --sort=-v:refname | head -1)
  echo "Recent tags:"
  git tag --sort=-v:refname | head -5
  echo ""

  local version
  version=$(choose_version "$last_tag" "$preselect")
  [ -n "$version" ] || { echo "Error: no version chosen" >&2; exit 1; }

  echo ""
  echo "== Changes since ${last_tag} =="
  git log "${last_tag}..HEAD" --oneline

  echo ""
  echo "== Draft the end-user summary (opens \$EDITOR) =="
  local summary
  summary=$(edit_summary "$last_tag")
  if [ -z "$(echo "$summary" | tr -d '[:space:]')" ]; then
    echo "Error: summary is empty — aborting" >&2
    exit 1
  fi

  echo ""
  echo "== Proposed release =="
  echo "Version: $version"
  echo "Summary:"
  echo "$summary"
  echo ""
  read -r -p "Proceed with changelog PR for $version? [y/N] " confirm
  [ "$confirm" = "y" ] || [ "$confirm" = "Y" ] || { echo "Aborted."; exit 1; }

  local v=${version#v}
  local branch="chore/changelog-${v}"
  git switch -c "$branch"

  local changelog
  changelog="$(git rev-parse --show-toplevel)/CHANGELOG.md"
  local today
  today=$(date +%Y-%m-%d)

  update_changelog "$changelog" "$v" "$today" "$last_tag" "$version" "$summary"

  echo ""
  echo "== CHANGELOG.md diff =="
  git diff -- CHANGELOG.md

  read -r -p "Commit, push, and open the changelog PR? [y/N] " confirm2
  [ "$confirm2" = "y" ] || [ "$confirm2" = "Y" ] || { echo "Aborted (CHANGELOG.md left edited on branch $branch)."; exit 1; }

  git add "$changelog"
  git commit -m "📝 docs: update changelog for ${version}"
  git push -u origin "$branch"
  gh pr create \
    --title "📝 docs: update changelog for ${version}" \
    --body "Changelog entry for the upcoming ${version} release."

  echo ""
  echo "PR opened. Watch CI, then merge it (squash), then run:"
  echo "  scripts/cut-release.sh tag ${version}"
}

cmd_tag() {
  local version=${1:-}
  [ -n "$version" ] || usage

  require_clean_main
  git fetch origin main
  if [ -n "$(git log HEAD..origin/main --oneline)" ]; then
    echo "Pulling latest main (changelog PR must already be merged)..."
    git pull
  fi

  if ! grep -q "## \[${version#v}\]" CHANGELOG.md; then
    echo "Error: CHANGELOG.md has no section for ${version#v} — merge the changelog PR first" >&2
    exit 1
  fi

  echo "== CHANGELOG.md section for ${version#v} =="
  awk -v ver="${version#v}" '
    $0 ~ "## \\[" ver "\\]" { found=1 }
    found && /^## \[/ && !($0 ~ "## \\[" ver "\\]") { exit }
    found { print }
  ' CHANGELOG.md

  echo ""
  read -r -p "Tag and push ${version}? This starts a public release. [y/N] " confirm
  [ "$confirm" = "y" ] || [ "$confirm" = "Y" ] || { echo "Aborted."; exit 1; }

  local summary_file
  summary_file=$(mktemp)
  awk -v ver="${version#v}" '
    $0 ~ "## \\[" ver "\\]" { found=1; next }
    found && /^## \[/ { exit }
    found { print }
  ' CHANGELOG.md | sed -e '/./,$!d' >"$summary_file"

  git tag -a "$version" -F "$summary_file"
  rm -f "$summary_file"

  git push origin "$version"

  echo ""
  echo "Pushed. Track the release:"
  echo "  Actions: https://github.com/${REPO}/actions"
  echo "  Releases: https://github.com/${REPO}/releases"
}

case "$cmd" in
  prepare) cmd_prepare "${1:-}" ;;
  tag) cmd_tag "${1:-}" ;;
  *) usage ;;
esac
