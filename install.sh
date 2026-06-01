#!/usr/bin/env bash
# install.sh — Download and install the latest BpMonitor release from GitHub.
#
# Usage:
#   ./install.sh [OPTIONS]
#
# Options:
#   -t TARGET Which app to install: tui or web    (default: tui)
#   -b PATH   Base directory for installation      (default: ~/.local/bin)
#   -d NAME   Subdirectory under base path         (default: bp for tui, bpweb for web)
#   -n NAME   Name of the installed executable     (default: bpmonitor / bpmonitor-web)
#   -h        Show this help message
#
# Options take precedence over environment variables of the same meaning.
# The binary is installed to: BASE_PATH/INSTALL_DIR_NAME/BINARY_NAME
# An appsettings.json is placed alongside the binary.
#
# The web target is distributed as a tarball bundling the single-file binary,
# its wwwroot/ static assets, and appsettings.json. Run it from its install
# directory so the static assets are found, e.g.:
#   cd ~/.local/bin/bpweb && ./bpmonitor-web
#
# Examples:
#   ./install.sh
#   ./install.sh -t web
#   ./install.sh -b /opt/local/bin
#   ./install.sh -d bpmon -n bp
set -euo pipefail

REPO="draptik/BpMonitor"
TARGET="${TARGET:-tui}"
BASE_PATH="${BASE_PATH:-$HOME/.local/bin}"
# Left empty so target-specific defaults below only apply when the user
# did not override them via an option or environment variable.
INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-}"
BINARY_NAME="${BINARY_NAME:-}"

usage() {
  sed -n '/^# install\.sh/,/^[^#]/{ /^[^#]/d; s/^# \?//; p }' "$0"
  exit 0
}

while getopts ":t:b:d:n:h" opt; do
  case $opt in
    t) TARGET="$OPTARG" ;;
    b) BASE_PATH="$OPTARG" ;;
    d) INSTALL_DIR_NAME="$OPTARG" ;;
    n) BINARY_NAME="$OPTARG" ;;
    h) usage ;;
    :) echo "Option -$OPTARG requires an argument." >&2; exit 1 ;;
    \?) echo "Unknown option: -$OPTARG" >&2; exit 1 ;;
  esac
done

case "$TARGET" in
  tui)
    ARTIFACT_NAME="bpmonitor"
    INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-bp}"
    BINARY_NAME="${BINARY_NAME:-bpmonitor}"
    ;;
  web)
    ARTIFACT_NAME="bpmonitor-web-linux-x64.tar.gz"
    INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-bpweb}"
    BINARY_NAME="${BINARY_NAME:-bpmonitor-web}"
    ;;
  *)
    echo "Unknown target: $TARGET (expected 'tui' or 'web')" >&2
    exit 1
    ;;
esac

INSTALL_DIR="$BASE_PATH/$INSTALL_DIR_NAME"
INSTALL_PATH="$INSTALL_DIR/$BINARY_NAME"

LATEST=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep tag_name | cut -d'"' -f4)
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST/$ARTIFACT_NAME"

mkdir -p "$INSTALL_DIR"

case "$TARGET" in
  tui)
    curl -fsSL "$DOWNLOAD_URL" -o "$INSTALL_PATH"
    chmod +x "$INSTALL_PATH"
    curl -fsSL "https://github.com/$REPO/releases/download/$LATEST/appsettings.json" -o "$INSTALL_DIR/appsettings.json"
    ;;
  web)
    tarball=$(mktemp)
    curl -fsSL "$DOWNLOAD_URL" -o "$tarball"
    # Tarball contains: bpmonitor-web, appsettings.json, wwwroot/
    tar -xzf "$tarball" -C "$INSTALL_DIR"
    rm -f "$tarball"
    # Honour a custom binary name by renaming the extracted executable.
    if [ "$BINARY_NAME" != "bpmonitor-web" ]; then
      mv "$INSTALL_DIR/bpmonitor-web" "$INSTALL_PATH"
    fi
    chmod +x "$INSTALL_PATH"
    ;;
esac

echo "Installed $BINARY_NAME $LATEST to $INSTALL_PATH"
if [ "$TARGET" = "web" ]; then
  echo "Run it from its install directory so static assets resolve:"
  echo "  cd $INSTALL_DIR && ./$BINARY_NAME"
fi
