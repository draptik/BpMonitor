# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Install guide now documents manual tarball download and Docker Compose; the `install.sh` script has been removed.

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

[Unreleased]: https://github.com/draptik/BpMonitor/compare/v1.4.2...HEAD
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
