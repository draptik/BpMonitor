# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- History chart's legend moved from the default top-right position to a horizontal bar centered below the chart, matching the Trends and Recent charts

## [1.7.0-rc3] - 2026-06-24

### Added

- New icon-button navigation on the landing page for trends, recent, settings, exports, and members

### Changed

- Recent page's chart no longer sits behind a "Blood Pressure Graph" collapse toggle — it now renders directly under the page heading
- Recent and History charts: removed the lasso, autoscale and box-select buttons from Plotly's toolbar, and locked the y-axis so zoom can no longer stretch or compress the blood-pressure scale (only the x-axis is zoomable)
- Recent page now loads the last 12 months of readings into the chart and value strip, opening focused on the last 30 days — pan left to see older readings instead of them being hidden entirely
- Recent page's value strip stays in sync with the chart when zooming or panning, instead of always showing the last 30 days

## [1.7.0-rc2] - 2026-06-23

### Fixed

- Recent chart's LOWESS trend line no longer breaks into gaps when readings cluster on the same day — a zero-weight local fit was dividing by zero and producing NaN

## [1.7.0-rc1] - 2026-06-23

### Added

- Recent chart now overlays a LOWESS-smoothed trend line for systolic and diastolic; the trend line is the visual focus (full color, thicker) and the raw per-reading line is faded, matching the Wegier et al. 2021 design
- Recent page now shows a Fig. 5-style "value strip" directly above the chart, listing every Systolic/Diastolic value in the chart's 30-day window in chronological order, sized to match the chart's width with no horizontal scrolling
- Demo dataset gains a sixth member, Ned Flanders, whose readings tell a Fig. 5-style story (elevated readings, a week-long gap, then improved control after starting medication) to showcase the new trend line and missing-data dashing
- Recent chart now has a Fig. 5-style scrubber bar: hovering shows a vertical line that snaps to the nearest reading and boxes the matching column in the value strip above the chart, linking the two together
- Recent page's value strip now color-codes each value against the member's goal range, matching Fig. 5: above the goal max renders orange, below the goal min renders blue, in-range values stay neutral

### Changed

- History page heading no longer repeats the family member's name
- Adding a new reading now redirects to the **Recent** page instead of **History**
- History and Recent charts no longer show a "Blood Pressure History" title, matching the Trends chart, and now use the freed-up vertical space with more compact margins
- Sidebar navigation links on mobile have more spacing between them, making them easier to tap accurately
- Redesigned the hamburger menu and theme toggle to fit the rest of the design: mobile now has a slim top app bar (menu, title, theme toggle) instead of floating corner buttons, and the desktop theme toggle now lives in the sidebar next to the member name

## [1.6.0] - 2026-06-21

### Added

- New **Recent** page (`/recent`) showing raw readings from the last 7, 14, and 30 days as three separate tables, plus a 30-day chart — no aggregates
- Charts now show a color-coded goal range band for systolic and diastolic readings, personalizable per family member at the new **Settings** page (`/settings`); defaults to 90–140 (systolic) and 60–90 (diastolic)

### Changed

- Charts now load plotly.js from a locally vendored copy instead of the `cdn.plot.ly` CDN — no third-party request needed to view charts
- Systolic/diastolic chart colors changed to a colorblind-safe mint/cocoa palette to match the new goal-range bands
- Charts now show denser y-axis gridlines, visible x/y axis lines and tick marks that adapt to the light/dark theme, and a "blood pressure [mmHg]" y-axis label
- History and Recent charts now mark every reading with a circle, and comment markers sit on the x-axis baseline instead of overlapping the systolic line
- Recent chart now shows a dashed connecting line across gaps where data is missing (more than 10% of the displayed 30-day window), with its legend styling matching the History chart

### Fixed

- Charts (History, Recent, Trends) no longer clip their x-axis date labels on narrow mobile screens — Plotly was rendering at a fixed default height regardless of the actual container size, and the excess was silently cut off

## [1.5.1] - 2026-06-16

### Changed

- Internal code quality improvements and refactoring (no user-facing changes).

## [1.5.0] - 2026-06-16

### Added

- Trends chart is now rendered inline — no iframe, no fixed-height clipping, smoother on all screen sizes
- Trends chart colours improved: systolic trace in green, error bars dimmed, hovered point highlighted
- Period pills with no readings are now visibly grayed out and unclickable

## [1.4.3] - 2026-06-15

### Changed

- **Deployment:** `install.sh` has been removed. Install by downloading and extracting the release tarball manually, or use Docker Compose — see the updated [install guide](https://github.com/draptik/BpMonitor/blob/main/docs/install.md).

## [1.4.2] - 2026-06-15

### Added

- Added `CHANGELOG.md` — version history is now tracked in the repository.

## [1.4.1] - 2026-06-15

### Changed

- Internal tooling improvements (no user-facing changes).

## [1.4.0] - 2026-06-15

### Fixed

- Chart height rendering in the history view is now correct.

### Changed

- Pico CSS is now vendored locally — no CDN request at startup.
  No action needed; existing deployments update automatically on upgrade.

## [1.3.0] - 2026-06-14

### Changed

- Internal code organisation improvements (no user-facing changes).

## [1.2.1] - 2026-06-13

### Changed

- Internal code organisation improvements (no user-facing changes).

## [1.2.0] - 2026-06-12

### Changed

- Test suite now runs all projects in parallel, significantly reducing local CI time.

## [1.1.0] - 2026-06-12

### Added

- Granularity selector on the history chart: switch between Weekly, Monthly, and Yearly views.
- Horizontally-scrollable sub-period row (12 weeks / 12 months / 5 years) with chronological navigation.
- Chart aggregation and X-axis labels adapt per granularity (daily / weekly / monthly averages).

## [1.0.1] - 2026-06-10

### Fixed

- Version number in the footer now displays correctly for a tagged `v1.0.0` release
  (previously it was shown as "dev").

## [1.0.0] - 2026-06-09

First stable release of the BpMonitor web app.

### Added

- `/trends` page: windowed daily-grouped chart for spotting long-term trends.
- Admin and Active flags for family members, with an admin edit flow.

### Fixed

- Trends chart hover tooltip no longer shows the reading count once per trace.

## [0.1.14] - 2026-06-04

### Changed

- MIT license now mentioned explicitly in contribution docs.

## [0.1.13] - 2026-06-01

### Changed

- Release assets restructured: TUI and web app now ship as separate named tarballs.

## [0.1.12] - 2026-06-01

### Added

- Web app container image published to `ghcr.io/draptik/bpmonitor-web` on every release.
- Web install paths added to `install.sh`.

## [0.1.11] - 2026-06-01

### Changed

- Pre-release tags (containing `-`) are now correctly flagged as GitHub pre-releases,
  keeping `/releases/latest` on the stable release.

## [0.1.10] - 2026-04-29

### Changed

- Dependency update (no user-facing changes).

## [0.1.9] - 2026-04-14

### Changed

- Internal refactoring (no user-facing changes).

## [0.1.8] - 2026-04-14

### Changed

- Install documentation updated; helper scripts added.

## [0.1.7] - 2026-04-14

### Changed

- `install.sh` now accepts CLI arguments for a configurable install.

## [0.1.6] - 2026-04-13

### Fixed

- Chart and import timestamps now display in local time instead of UTC.

## [0.1.5] - 2026-04-13

### Fixed

- Tilde (`~`) in the `Import:MarkdownDirectory` config path is now expanded correctly
  before the file dialog opens.

## [0.1.4] - 2026-04-13

### Added

- Serilog file logging with a configurable path via `appsettings.json`.

## [0.1.3] - 2026-04-13

### Added

- `Import:MarkdownDirectory` config key in `appsettings.json` (default: current directory).

## [0.1.2] - 2026-04-13

### Changed

- Internal test refactoring (no user-facing changes).

## [0.1.1] - 2026-04-13

### Fixed

- `appsettings.json` is now included in release assets and picked up by `install.sh`.

## [0.1.0] - 2026-04-13

### Added

- Initial GitHub release workflow and `install.sh` for automated deployment.

[Unreleased]: https://github.com/draptik/BpMonitor/compare/v1.7.0-rc3...HEAD
[1.7.0-rc3]: https://github.com/draptik/BpMonitor/compare/v1.7.0-rc2...v1.7.0-rc3
[1.7.0-rc2]: https://github.com/draptik/BpMonitor/compare/v1.7.0-rc1...v1.7.0-rc2
[1.7.0-rc1]: https://github.com/draptik/BpMonitor/compare/v1.6.0...v1.7.0-rc1
[1.6.0]: https://github.com/draptik/BpMonitor/compare/v1.5.1...v1.6.0
[1.5.1]: https://github.com/draptik/BpMonitor/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/draptik/BpMonitor/compare/v1.4.3...v1.5.0
[1.4.3]: https://github.com/draptik/BpMonitor/compare/v1.4.2...v1.4.3
[1.4.2]: https://github.com/draptik/BpMonitor/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/draptik/BpMonitor/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/draptik/BpMonitor/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/draptik/BpMonitor/compare/v1.2.1...v1.3.0
[1.2.1]: https://github.com/draptik/BpMonitor/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/draptik/BpMonitor/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/draptik/BpMonitor/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/draptik/BpMonitor/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/draptik/BpMonitor/compare/v0.1.14...v1.0.0
[0.1.14]: https://github.com/draptik/BpMonitor/compare/v0.1.13...v0.1.14
[0.1.13]: https://github.com/draptik/BpMonitor/compare/v0.1.12...v0.1.13
[0.1.12]: https://github.com/draptik/BpMonitor/compare/v0.1.11...v0.1.12
[0.1.11]: https://github.com/draptik/BpMonitor/compare/v0.1.10...v0.1.11
[0.1.10]: https://github.com/draptik/BpMonitor/compare/v0.1.9...v0.1.10
[0.1.9]: https://github.com/draptik/BpMonitor/compare/v0.1.8...v0.1.9
[0.1.8]: https://github.com/draptik/BpMonitor/compare/v0.1.7...v0.1.8
[0.1.7]: https://github.com/draptik/BpMonitor/compare/v0.1.6...v0.1.7
[0.1.6]: https://github.com/draptik/BpMonitor/compare/v0.1.5...v0.1.6
[0.1.5]: https://github.com/draptik/BpMonitor/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/draptik/BpMonitor/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/draptik/BpMonitor/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/draptik/BpMonitor/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/draptik/BpMonitor/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/draptik/BpMonitor/releases/tag/v0.1.0
