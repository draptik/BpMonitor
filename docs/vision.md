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

### Export

- CSV export (nice-to-have)

## Usage

- Primary user: personal use only, single user
- Logging frequency: sporadic (multiple times/day or gaps of several days)
- Devices: phone (input) and desktop TUI (input + viewing) — both talk to the API

## Hosting

- Self-hosted on a Proxmox VM — always-on, home network only
- No external access, no authentication required
- Database: PostgreSQL (replacing SQLite)
- The API is the single source of truth

## Architecture (high-level)

- **API** — hosted on Proxmox VM; accepts readings, serves data to all clients
- **Mobile web UI** — mobile-optimised web page for quick input (systolic, diastolic, heart rate, optional comment); shows success/failure after submit; first iteration only, no charts
- **TUI** — connects to the API as a client; no longer hosts the database directly

## Success Criteria

- Easy to log a reading quickly from phone or desktop
- Charts make trends and anomalies visible at a glance
- Comments allow context to be attached to unusual readings
