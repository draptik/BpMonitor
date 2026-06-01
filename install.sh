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
#   -n NAME   Name of the installed executable     (default: bpmonitor-tui / bpmonitor-web)
#   -h        Show this help message
#
# Options take precedence over environment variables of the same meaning.
# The binary is installed to: BASE_PATH/INSTALL_DIR_NAME/BINARY_NAME
#
# Each target is distributed as a self-contained tarball
# (bpmonitor-<target>-linux-x64.tar.gz) bundling the single-file binary and its
# appsettings.json — the web bundle additionally carries its wwwroot/ static
# assets, so run the web binary from its install directory, e.g.:
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
    DEFAULT_BINARY="bpmonitor-tui"
    INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-bp}"
    ;;
  web)
    DEFAULT_BINARY="bpmonitor-web"
    INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-bpweb}"
    ;;
  *)
    echo "Unknown target: $TARGET (expected 'tui' or 'web')" >&2
    exit 1
    ;;
esac

ARTIFACT_NAME="$DEFAULT_BINARY-linux-x64.tar.gz"
BINARY_NAME="${BINARY_NAME:-$DEFAULT_BINARY}"
INSTALL_DIR="$BASE_PATH/$INSTALL_DIR_NAME"
INSTALL_PATH="$INSTALL_DIR/$BINARY_NAME"

LATEST=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep tag_name | cut -d'"' -f4)
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST/$ARTIFACT_NAME"

mkdir -p "$INSTALL_DIR"

# Both targets ship as tarballs (binary + appsettings.json [+ wwwroot/]).
tarball=$(mktemp)
curl -fsSL "$DOWNLOAD_URL" -o "$tarball"
tar -xzf "$tarball" -C "$INSTALL_DIR"
rm -f "$tarball"

# Honour a custom binary name by renaming the extracted executable.
if [ "$BINARY_NAME" != "$DEFAULT_BINARY" ]; then
  mv "$INSTALL_DIR/$DEFAULT_BINARY" "$INSTALL_PATH"
fi
chmod +x "$INSTALL_PATH"

echo "Installed $BINARY_NAME $LATEST to $INSTALL_PATH"
if [ "$TARGET" = "web" ]; then
  echo "Run it from its install directory so static assets resolve:"
  echo "  cd $INSTALL_DIR && ./$BINARY_NAME"
fi
