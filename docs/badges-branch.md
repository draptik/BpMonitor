# The `badges` branch

The `badges` branch is an **orphan branch** (no shared history with `main`) used only as
storage for the generated coverage badge. It holds a single file, `coverage.svg`, which the
coverage badge in the README links to via `raw.githubusercontent.com`.

CI regenerates and force-pushes this file on every run (see the "Push coverage badge to badges
branch" step in `.github/workflows/ci.yml`). The branch is **intentionally never merged** into
`main` — keeping the generated artifact and its churn out of the source history. Treat it as
machine-owned: don't branch off it or commit to it. If deleted, the next CI run recreates it.
