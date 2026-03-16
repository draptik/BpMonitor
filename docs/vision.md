# Product Vision: Blood Pressure Monitor

## Purpose

A personal health tracking tool to log and visualize blood pressure data over time, with the ability to annotate readings and spot patterns or anomalies.

## Data Model

- Systolic (mmHg)
- Diastolic (mmHg)
- Heart rate (bpm)
- Timestamp
- Comments (for correlating readings with events)

## Features

### Input

- Simple, low-friction manual entry form

### Visualization

- Default overview charts (trends over time)
- Interactive charts for exploratory analysis (spot anomalies, correlate with comments/events)

### Import / Export

- JSON export on close / manual trigger (implemented)
- JSON import on open — merges readings not yet in the local database (implemented)
- Markdown import — bulk import from legacy log files (implemented)
- CSV export (nice-to-have)

## Usage

- Primary user: personal use only, single user
- Logging frequency: sporadic (multiple times/day or gaps of several days)
- Devices: phone (input) and desktop TUI (input + viewing) — both talk to the API

## Hosting

- No central server — each client is self-sufficient with its own local database
- Data stays on personal infrastructure; no cloud dependency
- Nextcloud is the transport layer for syncing between clients (via desktop sync client on TUI side, WebDAV API on PWA side)

## Architecture (high-level, idea stage)

- **TUI** — local SQLite; exports readings to a JSON file in a Nextcloud-watched folder on open/close or manual trigger
- **PWA (phone)** — local IndexedDB; reads/writes JSON via Nextcloud WebDAV API; installable, works offline
  — _spike abandoned: see [ADR-0001](adr/0001-pwa-phone-client-spike.md)_
- **Sync** — append-only merge: each client imports readings it hasn't seen before from other clients' JSON files; no conflict resolution needed
- **Nextcloud** — file transport only; no custom API required

## Success Criteria

- Easy to log a reading quickly from phone or desktop
- Charts make trends and anomalies visible at a glance
- Comments allow context to be attached to unusual readings
