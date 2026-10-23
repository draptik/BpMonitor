# Product Vision: Blood Pressure Monitor

## Purpose

A personal health tracking tool to log and visualize blood pressure data over time, with the ability to annotate readings and spot patterns or anomalies.

## Users

- Family / household members, each with their own readings, goal range, and language
- Logging is sporadic: several times a day, or gaps of several days
- Any browser, phone or desktop

## Features

- **Input:** a simple, low-friction entry form — systolic, diastolic, heart rate, timestamp, and an optional comment for correlating readings with events
- **Visualization:** overview charts of trends over time, plus interactive charts for spotting anomalies and correlating them with comments and medication changes
- **Export:** JSON and CSV, so the data never gets locked in

## Hosting

- Self-hosted web server with a single SQLite database
- Data stays on personal infrastructure; no cloud dependency

## Success Criteria

- Easy to log a reading quickly from phone or desktop
- Charts make trends and anomalies visible at a glance
- Comments allow context to be attached to unusual readings
