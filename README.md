# BpMonitor

[![CI](https://github.com/draptik/BpMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/draptik/BpMonitor/actions/workflows/ci.yml)
[![Coverage](https://raw.githubusercontent.com/draptik/BpMonitor/badges/coverage.svg)](https://github.com/draptik/BpMonitor/actions)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A blood pressure monitoring application for families and households, built with F# on .NET. Ships a multi-user web frontend (Falco) backed by a SQLite database.

## Screenshots

| | |
| --- | --- |
| ![Landing page](docs/screenshots/landing-light.png) | ![History](docs/screenshots/history-light.png) |
| ![Recent readings with scrubber](docs/screenshots/recent-scrubber-light.png) | ![Trends](docs/screenshots/trends-light.png) |

## Docs

- [Vision](docs/vision.md) — what the app does and why
- [Architecture](docs/architecture.md) — tech stack and structural decisions
- [Install](docs/install.md) — install, configure, and deploy
- [Contributing](CONTRIBUTING.md) — build, test, and release the project
- [Badges branch](docs/badges-branch.md) — how the coverage badge is generated and stored
- [Security](SECURITY.md) — how to report a vulnerability

## About This Project

This project was created with extensive assistance from [Claude](https://claude.ai) (Anthropic's AI assistant). Claude contributed throughout the development process — from architecture decisions and domain modelling to writing code, tests, and tooling configuration.

For details on how Claude was used and configured for this project, see [AGENTS.md](AGENTS.md).
