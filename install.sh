#!/usr/bin/env bash
# install.sh — Download and install the latest BpMonitor release from GitHub.
#
# Usage:
#   ./install.sh [OPTIONS]
#
# Options:
#   -b PATH   Base directory for installation  (default: ~/.local/bin)
#   -d NAME   Subdirectory under base path     (default: bp)
#   -n NAME   Name of the installed executable (default: bpmonitor)
#   -h        Show this help message
#
# Options take precedence over environment variables of the same meaning.
# The binary is installed to: BASE_PATH/INSTALL_DIR_NAME/BINARY_NAME
# An appsettings.json is placed alongside the binary.
#
# Examples:
#   ./install.sh
#   ./install.sh -b /opt/local/bin
#   ./install.sh -d bpmon -n bp
set -euo pipefail

REPO="draptik/BpMonitor"
ARTIFACT_NAME="bpmonitor"
BASE_PATH="${BASE_PATH:-$HOME/.local/bin}"
INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-bp}"
BINARY_NAME="${BINARY_NAME:-$ARTIFACT_NAME}"

usage() {
  sed -n '/^# install\.sh/,/^[^#]/{ /^[^#]/d; s/^# \?//; p }' "$0"
  exit 0
}

while getopts ":b:d:n:h" opt; do
  case $opt in
    b) BASE_PATH="$OPTARG" ;;
    d) INSTALL_DIR_NAME="$OPTARG" ;;
    n) BINARY_NAME="$OPTARG" ;;
    h) usage ;;
    :) echo "Option -$OPTARG requires an argument." >&2; exit 1 ;;
    \?) echo "Unknown option: -$OPTARG" >&2; exit 1 ;;
  esac
done
INSTALL_DIR="$BASE_PATH/$INSTALL_DIR_NAME"
INSTALL_PATH="$INSTALL_DIR/$BINARY_NAME"

LATEST=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep tag_name | cut -d'"' -f4)
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST/$ARTIFACT_NAME"

mkdir -p "$INSTALL_DIR"

curl -fsSL "$DOWNLOAD_URL" -o "$INSTALL_PATH"
chmod +x "$INSTALL_PATH"

curl -fsSL "https://github.com/$REPO/releases/download/$LATEST/appsettings.json" -o "$INSTALL_DIR/appsettings.json"

echo "Installed $BINARY_NAME $LATEST to $INSTALL_PATH"
