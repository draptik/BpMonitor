# BpMonitor

[![CI](https://github.com/draptik/BpMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/draptik/BpMonitor/actions/workflows/ci.yml)
[![Coverage](https://raw.githubusercontent.com/draptik/BpMonitor/badges/coverage.svg)](https://github.com/draptik/BpMonitor/actions)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

A personal blood pressure monitoring application.

## About This Project

This project was created with extensive assistance from [Claude](https://claude.ai) (Anthropic's AI assistant). Claude contributed throughout the development process — from architecture decisions and domain modelling to writing code, tests, and tooling configuration.

For details on how Claude was used and configured for this project, see [AGENTS.md](AGENTS.md).

## Documentation

- [Vision](docs/vision.md) — what the app does and why
- [Architecture](docs/architecture.md) — tech stack and structural decisions

## The `badges` branch

The `badges` branch is an **orphan branch** (no shared history with `main`) used only as
storage for the generated coverage badge. It holds a single file, `coverage.svg`, which the
coverage badge above links to via `raw.githubusercontent.com`.

CI regenerates and force-pushes this file on every run (see the "Push coverage badge to badges
branch" step in `.github/workflows/ci.yml`). The branch is **intentionally never merged** into
`main` — keeping the generated artifact and its churn out of the source history. Treat it as
machine-owned: don't branch off it or commit to it. If deleted, the next CI run recreates it.
