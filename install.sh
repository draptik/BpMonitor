#!/usr/bin/env bash
set -euo pipefail

REPO="draptik/BpMonitor"
BINARY_NAME="bpmonitor"
INSTALL_PATH="${INSTALL_PATH:-$HOME/.local/bin/$BINARY_NAME}"

LATEST=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep tag_name | cut -d'"' -f4)
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST/$BINARY_NAME"

INSTALL_DIR="$(dirname "$INSTALL_PATH")"
mkdir -p "$INSTALL_DIR"

curl -fsSL "$DOWNLOAD_URL" -o "$INSTALL_PATH"
chmod +x "$INSTALL_PATH"

curl -fsSL "https://github.com/$REPO/releases/download/$LATEST/appsettings.json" -o "$INSTALL_DIR/appsettings.json"

echo "Installed $BINARY_NAME $LATEST to $INSTALL_PATH"
