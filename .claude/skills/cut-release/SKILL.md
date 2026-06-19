---
name: cut-release
description: Cut a BpMonitor release — gather changes since the last tag, propose an end-user summary, confirm it, then create an annotated tag whose message renders above the auto-generated changelog. Invoke when creating/cutting a release or pushing a version tag.
argument-hint: [version, e.g. v1.4.0]
model: claude-sonnet-4-6
---

# Cut Release

Guide the user through a safe, repeatable BpMonitor release. The tag annotation
message becomes the end-user summary displayed **above** the auto-generated
changelog on the GitHub release page.

A standalone script mirroring this flow lives at `scripts/cut-release.sh` —
use it directly (no Claude needed) via `scripts/cut-release.sh prepare` /
`scripts/cut-release.sh tag vX.Y.Z`. Keep both in sync when changing the process.

## Step 1 — Preflight

Run these checks and **abort if any fails**:

```bash
git branch --show-current               # must be main
git status --short                      # must be empty (clean tree)
git fetch origin main
git log HEAD..origin/main --oneline     # must be empty (up to date)
gh run list --branch main --limit 3    # latest CI run must show ✓ completed/success
```

Inform the user of any failures before proceeding.

## Step 2 — Determine version

```bash
git tag --sort=-v:refname | head -5    # inspect recent tags
```

Calculate the three candidate versions from the latest tag, then **ask the user
which version to use** — even if `$ARGUMENTS` already contains one (treat it as
a default pre-selection, not a skip). Present the options like this:

> Current version: **vX.Y.Z**
>
> Which version bump?
>
> | Option | Next version | When to use |
> | --- | --- | --- |
> | patch | vX.Y.(Z+1) | bug fixes only |
> | minor | vX.(Y+1).0 | new user-facing features |
> | major | v(X+1).0.0 | breaking changes |
> | pre-release | vX.Y.Z-rc1 | release candidate (won't become `/releases/latest`) |
>
> Recommendation: **minor** ← (or whichever fits the changes; justify briefly)
>
> Enter a version or press Enter to accept the recommendation:

Pre-release suffix `-rcN` is allowed — the workflow automatically marks
`-`-tags as GitHub prereleases so `/releases/latest` stays on the stable release.

## Step 3 — Gather changes

```bash
LAST_TAG=$(git tag --sort=-v:refname | head -1)
git log "${LAST_TAG}..HEAD" --oneline
```

Optionally cross-reference merged PRs for richer descriptions:

```bash
gh pr list --state merged --base main --limit 30 --json number,title,mergedAt \
  | jq --arg since "$(git log -1 --format=%aI "${LAST_TAG}")" \
       '[.[] | select(.mergedAt > $since)]'
```

## Step 4 — Draft the end-user summary

Write concise markdown covering **only what matters to end users and operators**.

**Include:**
- New user-facing features
- Deployment notes: breaking changes, schema/data migrations, new required env
  vars or config keys, renamed artifacts or changed URLs

**Exclude:** refactors, test-only changes, internal chores, CI tweaks, doc fixes,
dependency bumps (unless they change runtime behaviour).

Suggested shape — a single flat list so bullets render without gaps. Prefix
operator items with `**Deployment:**` (omit if nothing actionable):

```markdown
### What's new

- <new feature visible to the user>
- <another feature>
- **Deployment:** <something the operator must do or know — only if applicable>
```

Keep it short. Two or three bullet points is better than an essay.

This same set of bullets will populate `CHANGELOG.md` in Step 5a — author them
once, reuse for both.

## Step 5 — Confirm

Present the proposed **version** and **summary** to the user. Wait for explicit
approval or edits. Do **not** proceed to tagging until confirmed.

## Step 5a — Update CHANGELOG.md

Direct commits to `main` are blocked, so the changelog update must go through a
branch + PR. Do this **before** creating the tag.

```bash
git switch -c chore/changelog-vX.Y.Z
```

Edit `CHANGELOG.md`:

1. Leave `## [Unreleased]` at the top but clear its contents (keep the heading,
   leave the body empty).
2. Insert a new version section immediately after it, using the approved summary
   bullets grouped under `### Added`, `### Changed`, or `### Fixed` as
   appropriate. Use today's date in ISO format (`YYYY-MM-DD`):

   ```markdown
   ## [X.Y.Z] - YYYY-MM-DD

   ### Added

   - <bullet from approved summary>
   ```

3. Update the link-reference block at the bottom of the file:
   - Change `[Unreleased]` to compare the new tag to `HEAD`:
     `[Unreleased]: https://github.com/draptik/BpMonitor/compare/vX.Y.Z...HEAD`
   - Add a new compare line for the new version above the previous latest:
     `[X.Y.Z]: https://github.com/draptik/BpMonitor/compare/vPREV...vX.Y.Z`

Commit, push, open a PR, and **merge it to `main`** before proceeding:

```bash
git add "$(git rev-parse --show-toplevel)/CHANGELOG.md"
git commit -m "📝 docs: update changelog for vX.Y.Z"
git push -u origin chore/changelog-vX.Y.Z
gh pr create --title "📝 docs: update changelog for vX.Y.Z" --body "Changelog entry for the upcoming vX.Y.Z release."
# wait for CI to pass, then merge
gh pr merge --squash
```

After the PR is merged, pull `main` so the tag lands on the changelog commit:

```bash
git switch main
git pull
```

## Step 6 — Create annotated tag and push

Write the approved summary to a temp file (avoids shell quoting issues), then tag:

```bash
SUMMARY_FILE=$(mktemp)
cat > "$SUMMARY_FILE" << 'SUMMARY_EOF'
<paste approved summary here>
SUMMARY_EOF

git tag -a vX.Y.Z -F "$SUMMARY_FILE"
rm "$SUMMARY_FILE"
```

Confirm once more before pushing — pushing the tag starts a public release:

```bash
git push origin vX.Y.Z
```

`release.yml` picks up the tag, reads its annotation as the release body, and
appends the auto-generated changelog beneath it.

## Step 7 — Verify

After pushing, share:

- The Actions run URL: `https://github.com/draptik/BpMonitor/actions`
- The draft release (appears once the workflow completes): `https://github.com/draptik/BpMonitor/releases`

Note: if the tag has a `-` suffix it will be marked as a **prerelease** and will
not become `/releases/latest` — correct behaviour for RCs.

## Rules

- NEVER push a tag without explicit user confirmation in Step 5 and Step 6.
- NEVER include internal/technical changes in the summary — end users and
  operators are the audience.
- NEVER create a release from a branch other than `main`.
- NEVER skip the preflight; failing CI on `main` means the release isn't ready.
- NEVER tag before the `CHANGELOG.md` PR (Step 5a) is merged; the tag annotation
  and the `CHANGELOG.md` version section must match.
