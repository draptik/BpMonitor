#!/usr/bin/env bash
# Generates a live HTML preview of the blood pressure chart and opens it in the browser.
# Invokes preview-chart.fsx with sample data and writes the output to /tmp/bpchart-preview.html.
# Note: the Verify snapshot files use scrubbed GUIDs and won't render — use this script instead.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="/tmp/bpchart-preview.html"

dotnet build "$SCRIPT_DIR/BpMonitor.Charts" --verbosity quiet

dotnet fsi "$SCRIPT_DIR/preview-chart.fsx" "$OUT"

xdg-open "$OUT"
