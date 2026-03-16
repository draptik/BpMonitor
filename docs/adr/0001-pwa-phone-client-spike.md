# ADR-0001: PWA Phone Client (Spike — Abandoned)

## Status

Abandoned

## Context

Evaluate a Progressive Web App (Bolero/Blazor WASM) as a phone-friendly frontend for entering and syncing blood pressure readings.

## What worked

- Bolero WASM scaffold running in the browser and installable on Android (Chrome/Firefox)
- Blood pressure entry form with IndexedDB persistence between sessions
- GitHub Pages deployment via CI
- PWA installability (web app manifest, service worker, icons)

## What did not work — sync

All evaluated sync options failed to meet the requirements (private health data, no always-on server, minimal user interaction on Android):

| Option | Outcome |
| --- | --- |
| Nextcloud WebDAV (direct) | CORS blocked — Hetzner StorageShare does not allow custom response headers |
| Nextcloud virtual folder via File System Access API | Android document provider does not support writable file streams |
| `showDirectoryPicker()` to local folder | Works, but Android requires folder re-selection on every browser session — too much friction |
| Web Share API → Nextcloud app | Too many manual steps |
| Self-hosted REST API | Requires always-on server and auth/security work |
| Cloudflare Worker | Rejected — private health data must not leave own infrastructure |
| Syncthing | Same `showDirectoryPicker()` session-permission limitation as above |

## Root cause

Android's security model requires a user gesture to grant file system access on every new browser session. The File System Access API does not persist permissions across sessions on Android Chrome, making seamless background sync impossible in a pure PWA without a reachable server endpoint.

## Decision

The PWA approach is abandoned.

## Consequences

A phone-friendly frontend requires either a native Android app (which can access the file system without these restrictions) or a lightweight always-on server that the PWA can reach directly.
