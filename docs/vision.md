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

- Users: family / household members, each with their own readings
- Logging frequency: sporadic (multiple times/day or gaps of several days)
- Devices: any browser (phone or desktop)

## Hosting

- Self-hosted web server with a single SQLite database
- Data stays on personal infrastructure; no cloud dependency

## Success Criteria

- Easy to log a reading quickly from phone or desktop
- Charts make trends and anomalies visible at a glance
- Comments allow context to be attached to unusual readings
