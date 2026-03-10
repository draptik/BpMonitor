#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="/tmp/bpchart-preview.html"

dotnet build "$SCRIPT_DIR/BpMonitor.Charts" --verbosity quiet

dotnet fsi "$SCRIPT_DIR/preview-chart.fsx" "$OUT"

xdg-open "$OUT"
